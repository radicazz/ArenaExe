using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuButtons : MonoBehaviour
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

