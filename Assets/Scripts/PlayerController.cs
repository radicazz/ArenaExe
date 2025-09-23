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
        _movementInput = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
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
        _rigidbody.AddForce(movement * _moveSpeed);
    }
}
