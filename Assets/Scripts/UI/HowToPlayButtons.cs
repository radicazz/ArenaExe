using UnityEngine;
using UnityEngine.SceneManagement; 

public class HowToPlayButtons : MonoBehaviour
{
    public void BackToStart()
    {
        Debug.Log("Returning to the Start scene...");
        UnityEngine.SceneManagement.SceneManager.LoadScene("Start Scene");
    }
}
