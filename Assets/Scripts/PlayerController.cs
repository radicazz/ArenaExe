using UnityEngine;

public class PlayerController : MonoBehaviour
{
    Rigidbody _rigidbody;

    [SerializeField]
    float _moveSpeed = 5f;

    Vector2 _movementInput;

    Camera _mainCamera;

    void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _mainCamera = Camera.main;
    }

    void Update()
    {
        if (gameObject.CompareTag("Player 1"))
        {
            float x = 0;
            if (Input.GetKey(KeyCode.D)) x += 1;
            if (Input.GetKey(KeyCode.A)) x -= 1;
            float y = 0;
            if (Input.GetKey(KeyCode.W)) y += 1;
            if (Input.GetKey(KeyCode.S)) y -= 1;
            _movementInput = new Vector2(x, y);
        }
        else if (gameObject.CompareTag("Player 2"))
        {
            float x = 0;
            if (Input.GetKey(KeyCode.RightArrow)) x += 1;
            if (Input.GetKey(KeyCode.LeftArrow)) x -= 1;
            float y = 0;
            if (Input.GetKey(KeyCode.UpArrow)) y += 1;
            if (Input.GetKey(KeyCode.DownArrow)) y -= 1;
            _movementInput = new Vector2(x, y);
        }
    }

    void FixedUpdate()
    {
        Vector3 forward = _mainCamera.transform.forward;
        forward.y = 0;
        forward.Normalize();

        Vector3 right = _mainCamera.transform.right;
        right.y = 0;
        right.Normalize();

        Vector3 movement = right * _movementInput.x + forward * _movementInput.y;
        if (movement != Vector3.zero)
        {
            movement.Normalize();
        }
        _rigidbody.linearVelocity = movement * _moveSpeed;
    }
}
