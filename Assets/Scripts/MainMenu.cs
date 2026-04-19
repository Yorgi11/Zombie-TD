using UnityEngine;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    public void PlayGame()
    {
        SceneManager.LoadScene("Bootstrap");
    }

    public void QuitGame()
    {
        Application.Quit();
        Debug.Log("Application Quit");
    }
    
}
