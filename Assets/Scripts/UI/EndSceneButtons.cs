using UnityEngine;
using UnityEngine.SceneManagement;

public class EndSceneButtons : MonoBehaviour
{
    public void RestartGame()
    {
        Debug.Log("Restarting the game...");
        SceneManager.LoadSceneAsync("Game Scene");
    }

    public void QuitGame()
    {
        Debug.Log("Quiting the game...");
        Application.Quit();
    }

    public void GoToStart()
    {
        Debug.Log("Going to Start Scene...");
        SceneManager.LoadSceneAsync("Start Scene");
    }
}
