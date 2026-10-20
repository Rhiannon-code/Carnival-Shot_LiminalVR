using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace IntuitiveDesigns.ShootingRange.EditorTools
{
    /// The range components keep their data in private [SerializeField] fields, which is what this
    /// reaches through. Nothing here overwrites a value that is not still at its default
    internal class Fields
    {
        private readonly SerializedObject _target;

        public Fields(Object target) { _target = new SerializedObject(target); }

        public float Float(string name) { return Find(name).floatValue; }
        public int Int(string name) { return Find(name).intValue; }
        public bool Bool(string name) { return Find(name).boolValue; }
        public Object Ref(string name) { return Find(name).objectReferenceValue; }

        public Fields Set(string name, float value) { Find(name).floatValue = value; return this; }
        public Fields Set(string name, int value) { Find(name).intValue = value; return this; }
        public Fields Set(string name, bool value) { Find(name).boolValue = value; return this; }
        public Fields Set(string name, Object value) { Find(name).objectReferenceValue = value; return this; }
        public Fields Set(string name, Vector3 value) { Find(name).vector3Value = value; return this; }

        public Fields SetArray(string name, IList<Object> values)
        {
            var array = Find(name);
            array.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];

            return this;
        }

        public SerializedProperty Find(string name)
        {
            var property = _target.FindProperty(name);
            if (property == null)
                Debug.LogError("[RangeSetup] " + _target.targetObject.GetType().Name + " has no field '" +
                               name + "'. The tool and the code have drifted apart.");

            return property;
        }

        public void Apply() { _target.ApplyModifiedPropertiesWithoutUndo(); }
    }

    internal static class RangeSetupUtil
    {
        public static Transform Child(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                if (parent.GetChild(i).name == name) return parent.GetChild(i);
            }

            return null;
        }

        /// GameObject.Find skips anything inactive, which would have this tool build a second set of
        /// ghost hands every time the first set happened to be switched off
        public static GameObject FindInScene(string name)
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            if (!scene.IsValid()) return null;

            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var item in root.GetComponentsInChildren<Transform>(true))
                {
                    if (item.name == name) return item.gameObject;
                }
            }

            return null;
        }

        /// Not "GetComponent() ?? AddComponent()": in the editor a missing component comes back as a
        /// fake null that ?? does not see, so nothing would ever be added
        public static T Ensure<T>(GameObject target) where T : Component
        {
            var existing = target.GetComponent<T>();
            return existing != null ? existing : target.AddComponent<T>();
        }

        public static T Load<T>(string path) where T : Object
        {
            return AssetDatabase.LoadAssetAtPath<T>(path);
        }

        /// The box a mesh occupies in some other object's space, which is how the signs are measured
        /// against the figure that lifts them
        /// Pass null for space to measure in true metres, which is what the offsets are in
        public static bool MeshBoundsIn(Transform space, Transform item, out Bounds bounds)
        {
            bounds = new Bounds();

            var filter = item.GetComponentInChildren<MeshFilter>(true);
            if (filter == null || filter.sharedMesh == null) return false;

            var local = filter.sharedMesh.bounds;
            bool first = true;

            for (int corner = 0; corner < 8; corner++)
            {
                var point = new Vector3(
                    (corner & 1) == 0 ? local.min.x : local.max.x,
                    (corner & 2) == 0 ? local.min.y : local.max.y,
                    (corner & 4) == 0 ? local.min.z : local.max.z);

                point = filter.transform.TransformPoint(point);
                if (space != null) point = space.InverseTransformPoint(point);

                if (first) { bounds = new Bounds(point, Vector3.zero); first = false; }
                else bounds.Encapsulate(point);
            }

            return true;
        }

        public static int Layer(string name)
        {
            int layer = LayerMask.NameToLayer(name);
            if (layer < 0) Debug.LogError("[RangeSetup] There is no '" + name + "' layer in this project.");

            return layer;
        }
    }
}
