using System.Linq;
using UnityEditor;
using UnityEngine;
using Racing.AI;

namespace Racing.EditorTools
{
    [CustomEditor(typeof(RaceCourse))]
    public class RaceCourseEditor : Editor
    {
        RaceCourse Course => (RaceCourse)target;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Race Setup Tools", EditorStyles.boldLabel);

            DrawValidation();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add Grid Slot"))
                AddGridSlot();
            if (GUILayout.Button("Auto-Arrange Grid"))
                AutoArrangeGrid();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Create Finish Line"))
                CreateFinishLine();
            if (GUILayout.Button("Create AI Path Here"))
                CreateAIPath();
            EditorGUILayout.EndHorizontal();

            var existingTrigger = Course.GetComponentInChildren<RaceTrigger>();
            using (new EditorGUI.DisabledScope(existingTrigger != null))
            {
                if (GUILayout.Button(existingTrigger != null ? "Trigger Already Added" : "Add Race Trigger Here"))
                    AddTrigger();
            }
        }

        void DrawValidation()
        {
            var def = Course.definition;
            if (def == null)
            {
                EditorGUILayout.HelpBox("No RaceDefinition assigned.", MessageType.Warning);
                return;
            }

            int expectedSlots = def.opponentCount + 1;
            int actualSlots = Course.gridSlots?.Count(s => s != null) ?? 0;
            if (actualSlots != expectedSlots)
                EditorGUILayout.HelpBox(
                    $"Grid has {actualSlots} slot(s), definition expects {expectedSlots} " +
                    $"(opponentCount {def.opponentCount} + player). Use Auto-Arrange Grid to fix.",
                    MessageType.Warning);

            if (Course.mainPath == null)
                EditorGUILayout.HelpBox("No main AI path assigned.", MessageType.Warning);

            if (Course.finishLine == null)
                EditorGUILayout.HelpBox("No finish line assigned.", MessageType.Warning);

            if (Course.GetComponentInChildren<RaceTrigger>() == null)
                EditorGUILayout.HelpBox(
                    "No RaceTrigger under this course -- players have no way to start " +
                    "this race yet. Use Add Race Trigger Here.",
                    MessageType.Warning);
        }

        void AddGridSlot()
        {
            Undo.RecordObject(Course, "Add Grid Slot");
            var go = new GameObject($"GridSlot_{(Course.gridSlots?.Length ?? 0)}");
            Undo.RegisterCreatedObjectUndo(go, "Add Grid Slot");
            go.transform.SetParent(Course.transform);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;

            var list = Course.gridSlots?.ToList() ?? new System.Collections.Generic.List<Transform>();
            list.Add(go.transform);
            Course.gridSlots = list.ToArray();
            EditorUtility.SetDirty(Course);
        }

        void AutoArrangeGrid()
        {
            var def = Course.definition;
            int slotCount = def != null ? def.opponentCount + 1 : Mathf.Max(1, Course.gridSlots?.Length ?? 1);

            Undo.RecordObject(Course, "Auto-Arrange Grid");

            // Destroy previously auto-generated slots that are direct children named GridSlot_*
            var existing = Course.gridSlots?.Where(t => t != null).ToList() ?? new System.Collections.Generic.List<Transform>();
            while (existing.Count < slotCount)
            {
                var go = new GameObject($"GridSlot_{existing.Count}");
                Undo.RegisterCreatedObjectUndo(go, "Auto-Arrange Grid");
                go.transform.SetParent(Course.transform);
                existing.Add(go.transform);
            }
            while (existing.Count > slotCount)
            {
                var t = existing[existing.Count - 1];
                existing.RemoveAt(existing.Count - 1);
                if (t != null) Undo.DestroyObjectImmediate(t.gameObject);
            }

            const float rowSpacing = 4f;
            const float colSpacing = 2.5f;
            for (int i = 0; i < existing.Count; i++)
            {
                int row = i / 2;
                int col = i % 2;
                float x = (col == 0 ? -1f : 1f) * (colSpacing * 0.5f);
                float z = -row * rowSpacing;
                existing[i].localPosition = new Vector3(x, 0f, z);
                existing[i].localRotation = Quaternion.identity;
                existing[i].name = i == 0 ? "GridSlot_Player" : $"GridSlot_{i}";
            }

            Course.gridSlots = existing.ToArray();
            EditorUtility.SetDirty(Course);
        }

        void CreateFinishLine()
        {
            Undo.RecordObject(Course, "Create Finish Line");
            var go = new GameObject("FinishLine");
            Undo.RegisterCreatedObjectUndo(go, "Create Finish Line");
            go.transform.SetParent(Course.transform);
            go.transform.localPosition = new Vector3(0f, 0f, 50f);
            Course.finishLine = go.transform;
            EditorUtility.SetDirty(Course);
        }

        void CreateAIPath()
        {
            Undo.RecordObject(Course, "Create AI Path");
            var go = new GameObject(Course.mainPath == null ? "AIPath_Main" : $"AIPath_Alt_{(Course.alternatePaths?.Length ?? 0)}");
            Undo.RegisterCreatedObjectUndo(go, "Create AI Path");
            go.transform.position = Course.transform.position;
            var path = go.AddComponent<SimpleAIPath>();

            // Seed two waypoints (start + 50m ahead) so the path is immediately valid;
            // use SimpleAIPathEditor's "Add Waypoint" to extend it.
            var wp0 = new GameObject("Waypoint_0").transform;
            wp0.SetParent(go.transform);
            wp0.position = go.transform.position;
            var wp1 = new GameObject("Waypoint_1").transform;
            wp1.SetParent(go.transform);
            wp1.position = go.transform.position + go.transform.forward * 50f;
            path.waypoints = new[] { wp0, wp1 };

            if (Course.mainPath == null)
            {
                Course.mainPath = path;
            }
            else
            {
                var list = Course.alternatePaths?.ToList() ?? new System.Collections.Generic.List<SimpleAIPath>();
                list.Add(path);
                Course.alternatePaths = list.ToArray();
            }
            EditorUtility.SetDirty(Course);
            Selection.activeGameObject = go;
        }

        void AddTrigger()
        {
            var triggerGO = new GameObject("TriggerArea");
            Undo.RegisterCreatedObjectUndo(triggerGO, "Add Race Trigger");
            triggerGO.transform.SetParent(Course.transform);
            triggerGO.transform.localPosition = Vector3.zero;
            triggerGO.transform.localRotation = Quaternion.identity;

            var col = Undo.AddComponent<BoxCollider>(triggerGO);
            col.isTrigger = true;
            col.size = new Vector3(14f, 4f, 14f);
            // Default the trigger a bit behind the player's own grid slot, along its
            // facing direction, so driving toward the start line enters it naturally.
            if (Course.gridSlots != null && Course.gridSlots.Length > 0 && Course.gridSlots[0] != null)
            {
                var slot = Course.gridSlots[0];
                Vector3 worldCenter = slot.position - slot.forward * 20f;
                col.center = triggerGO.transform.InverseTransformPoint(worldCenter);
            }

            Undo.AddComponent<RaceTrigger>(triggerGO);
            EditorUtility.SetDirty(triggerGO);
        }

        void OnSceneGUI()
        {
            if (Course.gridSlots != null)
            {
                foreach (var slot in Course.gridSlots)
                {
                    if (slot == null) continue;
                    EditorGUI.BeginChangeCheck();
                    Vector3 newPos = Handles.PositionHandle(slot.position, slot.rotation);
                    Quaternion newRot = Handles.RotationHandle(slot.rotation, slot.position);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(slot, "Move Grid Slot");
                        slot.position = newPos;
                        slot.rotation = newRot;
                    }
                }
            }

            if (Course.finishLine != null)
            {
                EditorGUI.BeginChangeCheck();
                Vector3 newPos = Handles.PositionHandle(Course.finishLine.position, Course.finishLine.rotation);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(Course.finishLine, "Move Finish Line");
                    Course.finishLine.position = newPos;
                }
                Handles.color = new Color(1f, 1f, 0f, 0.25f);
                Handles.DrawSolidDisc(Course.finishLine.position, Vector3.up, Course.finishLineRadius);
            }
        }
    }
}
