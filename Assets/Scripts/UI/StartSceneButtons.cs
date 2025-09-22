using UnityEngine;
using UnityEngine.SceneManagement;

public class StartSceneButtons : MonoBehaviour
{
    public void StartGame()
    {
        Debug.Log("Loading the game scene...");
        SceneManager.LoadScene("Game Scene");
    }

    public void QuitGame()
    {
        Debug.Log("Quiting the game...");
        Application.Quit();
    }
}

