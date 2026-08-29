using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Defeat : MonoBehaviour
{
    public void Quit()
    {
#if UNITY_EDITOR
// 在编辑器模式下停止运行
UnityEditor.EditorApplication.isPlaying = false;
#else
        // 在打包后的应用中退出程序
        Application.Quit();
#endif
    }
    public void Return()
    {
        SceneManager.LoadScene("MainMenu");
    }
}
