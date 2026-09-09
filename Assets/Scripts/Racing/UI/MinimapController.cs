using System.Collections.Generic;
using UnityEngine;
using MVC;
using MVC.Core;
using Racing.AI;

namespace Racing.UI
{
    /// Drives the MinimapCamera: an elevated, angled chase-style view (like the main
    /// follow camera, just higher and pulled back further) rather than a straight
    /// top-down look, rotating behind the player so their heading always reads as
    /// "away from camera". Also draws a route line along RaceCourse.mainPath's actual
    /// waypoints -- from the player to the finish -- while a race is active, NFS-style
    /// (a colored line following the road, not a straight as-the-crow-flies arrow). The
    /// camera, its RenderTexture and the circular UI panel that displays it are
    /// authored once in the Editor (see RaceUIController's "Build UI" and the
    /// MinimapCamera/GarageMarker scene objects) -- this component only moves the
    /// camera and updates the route line at runtime, it builds no UI.
    public class MinimapController : MonoBehaviour
    {
        static readonly Color RouteColor = new Color(0.15f, 0.95f, 0.35f);

        [SerializeField] Camera minimapCamera;
        [SerializeField] float heightAbove = 55f;
        [SerializeField] float distanceBack = 60f;
        [SerializeField] float lookAheadDistance = 30f;
        [SerializeField] float routeHeight = 0.3f;

        // The minimap is a second full-scene render every frame it fires -- measured
        // ~9ms/frame extra during a race (4 opponents), roughly halving framerate.
        // Rendering it manually at a reduced cadence instead of every Update is
        // imperceptible for a small rotating minimap and removes most of that cost.
        const int RenderEveryNFrames = 3;
        int frameCounter;

        Transform player;
        bool mainCameraFixed;
        LineRenderer routeLine;
        readonly List<Vector3> routePoints = new List<Vector3>();

        void Awake()
        {
            BuildRouteLine();
            // Camera.Render() is called manually below instead of relying on Unity's
            // automatic per-frame camera render.
            if (minimapCamera != null) minimapCamera.enabled = false;
        }

        void BuildRouteLine()
        {
            var go = new GameObject("RouteLine");
            go.layer = LayerMask.NameToLayer("Minimap");
            go.transform.SetParent(transform, false);

            routeLine = go.AddComponent<LineRenderer>();
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            routeLine.material = new Material(shader) { color = RouteColor };
            routeLine.startColor = routeLine.endColor = RouteColor;
            routeLine.startWidth = routeLine.endWidth = 1.2f;
            routeLine.useWorldSpace = true;
            routeLine.receiveShadows = false;
            routeLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            routeLine.enabled = false;
        }

        void LateUpdate()
        {
            // MVC's chase camera is spawned at runtime (Vehicle.Awake() bootstraps it)
            // with a default "render everything" culling mask, so it picks up the
            // Minimap-only layer (markers/route meant for the map camera alone) until
            // this strips it out. Runs once, retried each frame until that camera exists.
            if (!mainCameraFixed)
            {
                var mainCam = Camera.main;
                if (mainCam != null)
                {
                    mainCam.cullingMask &= ~(1 << LayerMask.NameToLayer("Minimap"));
                    mainCameraFixed = true;
                }
            }

            if (player == null)
            {
                var vm = FindFirstObjectByType<VehicleManager>();
                Vehicle v = vm != null ? vm.PlayerVehicle : null;
                player = v != null ? v.transform : null;
                if (player == null) return;
            }

            var pos = player.position;
            var forward = player.forward;

            if (minimapCamera != null)
            {
                var camPos = pos - forward * distanceBack + Vector3.up * heightAbove;
                minimapCamera.transform.position = camPos;
                minimapCamera.transform.LookAt(pos + forward * lookAheadDistance + Vector3.up * 1.5f);
            }

            frameCounter++;
            if (frameCounter % RenderEveryNFrames != 0) return;

            UpdateRoutePath(pos);
            if (minimapCamera != null) minimapCamera.Render();
        }

        void UpdateRoutePath(Vector3 playerPos)
        {
            var course = RaceManager.Instance != null ? RaceManager.Instance.ActiveCourse : null;
            SimpleAIPath path = course != null ? course.mainPath : null;
            if (path == null || !path.IsValid)
            {
                routeLine.enabled = false;
                return;
            }

            routePoints.Clear();
            routePoints.Add(playerPos + Vector3.up * routeHeight);

            int startIndex = path.ClosestPointIndex(playerPos);
            for (int i = startIndex; i < path.PointCount; i++)
                routePoints.Add(path.GetPoint(i) + Vector3.up * routeHeight);

            if (course.finishLine != null)
            {
                var finishPoint = course.finishLine.position + Vector3.up * routeHeight;
                if ((routePoints[routePoints.Count - 1] - finishPoint).sqrMagnitude > 1f)
                    routePoints.Add(finishPoint);
            }

            routeLine.enabled = true;
            routeLine.positionCount = routePoints.Count;
            routeLine.SetPositions(routePoints.ToArray());
        }
    }
}
