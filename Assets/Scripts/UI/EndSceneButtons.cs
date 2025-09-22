using UnityEngine;
using UnityEngine.SceneManagement;

public class EndSceneButtons : MonoBehaviour
{
    public void RestartGame()
    {
        Debug.Log("Restarting the game...");
        SceneManager.LoadScene("Game Scene");
    }

    public void QuitGame()
    {
        Debug.Log("Quiting the game...");
        Application.Quit();
    }
}
