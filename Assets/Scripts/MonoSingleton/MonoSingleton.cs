using UnityEngine;

public class MonoSingleton<T> : MonoBehaviour where T : MonoSingleton<T>
{
    private static T instance;
    public static T Instance
    {
        get
        {
            if (instance == null)
            {
                //在场景中根据类型查找引用
                instance = FindObjectOfType<T>();
                if (instance == null)
                {
                    //创建脚本对象（立即执行awake）
                    new GameObject("Singleton of" + typeof(T)).AddComponent<T>();
                }
                else
                {
                    instance.Init();
                }
            }
            return instance;
        }
    }

    protected void Awake()
    {
        if (instance == null)
        {
            instance = this as T;
            Init();
        }
    }

    public virtual void Init()
    {

    }
}
