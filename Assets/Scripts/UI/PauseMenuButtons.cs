using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenuButtons : MonoBehaviour
{
    public void ResumeGame()
    {
        Debug.Log("Resuming game...");

        ResumePausedState();
    }

    public void ExitGame()
    {
        Debug.Log("Exiting game...");
        SceneManager.LoadScene("Start Scene");
    }

    public void RestartLevel()
    {
        Debug.Log("Restarting level...");

        ResumePausedState();
        SceneManager.LoadScene("Game Scene");
    }

    void ResumePausedState()
    {
        GameState gameState = GameState.Instance;
        if (gameState != null)
        {
            if (gameState.IsPaused)
            {
                gameState.TogglePause();
            }
        }
        else
        {
            Time.timeScale = 1f;
        }
    }
}
