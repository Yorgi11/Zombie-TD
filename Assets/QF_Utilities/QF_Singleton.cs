using UnityEngine;
namespace QF_Tools.QF_Utilities
{
    public abstract class QF_Singleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        private static T s_instance;
        private static bool s_quitting;
        /// <summary>
        /// If true, the instance GameObject is marked DontDestroyOnLoad.
        /// </summary>
        protected virtual bool PersistAcrossScenes => true;
        public static T Instance
        {
            get
            {
                if (s_quitting) return null;
                if (!s_instance)
                {
                    s_instance = FindFirstObjectByType<T>();

                    // Only auto-create at runtime
                    if (!s_instance && Application.isPlaying)
                    {
                        GameObject obj = new(typeof(T).Name);
                        s_instance = obj.AddComponent<T>();

                        // If subclass wants persistence, mark it now
                        if (s_instance is QF_Singleton<T> singleton && singleton.PersistAcrossScenes) DontDestroyOnLoad(obj);
                    }
                }
                return s_instance;
            }
        }
        protected virtual void Awake()
        {
            if (s_quitting) return;
            if (!s_instance)
            {
                s_instance = this as T;
                if (PersistAcrossScenes && Application.isPlaying) DontDestroyOnLoad(gameObject);
            }
            else if (s_instance != this) Destroy(gameObject);
        }
        protected virtual void OnApplicationQuit()
        {
            s_quitting = true;
        }
        protected virtual void OnDestroy()
        {
            if (s_instance == this) s_instance = null;
        }
    }
}