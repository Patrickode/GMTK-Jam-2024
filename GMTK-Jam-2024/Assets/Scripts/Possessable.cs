using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class Possessable : MonoBehaviour
{
    [SerializeField] InputActionReference moveActionRef;
    [SerializeField] InputActionReference abilityActionRef;
    [Space(5)]
    [SerializeField] GroundChecker groundChecker;
    [SerializeField] IAbility abilityScript;

    [Header("Movement Variables")]
    [SerializeField] float maxSpeed;
    [SerializeField] float accModifier;
    [Space(5)]
    [SerializeField] float dirChangeThreshold = 0.01f;
    [Space(5)]
    [NaughtyAttributes.ShowNonSerializedField] Vector3 directionLastFrame;
#if UNITY_EDITOR
    [SerializeField] bool visualizeMoveInput;
#endif

    [Header("Other References")]
    [SerializeField] GameObject cameraPivot;
    [SerializeField] GameObject model;
    //[SerializeField] Animator anim;

    [Header("Rotation Controls")]
    [SerializeField] float rotationSpeed;
    [SerializeField] float minMoveBeforeRotate;

    Rigidbody rb;

    public InputActionReference AbilityActionRef => abilityActionRef;
    public GameObject Model => model;
    public Vector3 Velocity => rb.velocity;
    public bool GroundedCache { get; private set; }

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        moveActionRef.action.actionMap.Enable();
        abilityActionRef.action.performed += OnAbilityUsed;
    }
    private void OnDestroy()
    {
        abilityActionRef.action.performed -= OnAbilityUsed;
    }

    private void OnAbilityUsed(InputAction.CallbackContext ctx)
    {
        if (abilityScript != null) abilityScript.DoAbility();
    }



    void FixedUpdate()
    {
        ApplyMoveForce();
    }

    private void ApplyMoveForce()
    {
        if (maxSpeed <= 0) return;

        Vector3 direction = moveActionRef.action.ReadValue<Vector2>();

        // Multiplying direction by this quaternion makes it so a "forward" input is forward *according to the direction the camera pivot is facing*
        direction = Quaternion.LookRotation(Vector3.Cross(cameraPivot.transform.right, Vector3.up)) * direction.SwapAxes(1, 2);

        //anim.SetFloat("Walk Speed", direction.sqrMagnitude);

        bool movingEnoughToRotate = direction.sqrMagnitude > minMoveBeforeRotate * minMoveBeforeRotate;
        if (movingEnoughToRotate) RotateCharacterModel(direction.normalized);

        // If we have no grounded checker, ignore this.
        // If we do have one, are not grounded, and we're inputting significantly,
        if (groundChecker && !groundChecker.IsGrounded() && movingEnoughToRotate)
        {
            // Make velocity point in the direction of input if it's in a different direction (axis delta > deadzone),
            if (Mathf.Abs(directionLastFrame.x - direction.x) > dirChangeThreshold
                || Mathf.Abs(directionLastFrame.z - direction.z) > dirChangeThreshold)
            {
                // vel = input direction with magnitude of velocity's XZ (i.e., without Y),
                rb.velocity = direction * Vector3.ProjectOnPlane(rb.velocity, Vector3.up).magnitude;
                // with Y component equal to velocity's Y
                rb.velocity = rb.velocity.Adjust(1, rb.velocity.y);
            }
        }

        // If both axes are under max speed, apply force in direction.
        float percentHeld = direction.magnitude;
        if (Mathf.Abs(rb.velocity.x) < maxSpeed * percentHeld && Mathf.Abs(rb.velocity.z) < maxSpeed * percentHeld)
        {
            rb.AddForce(direction * accModifier, ForceMode.Force);
        }

        directionLastFrame = direction;
        DrawDebugMovementRays(direction);
    }



    private void RotateCharacterModel(Vector3 direction)
    {
        if (direction.sqrMagnitude <= Mathf.Epsilon) return;
        model.transform.rotation = Quaternion.LookRotation(new Vector3(direction.x, 0, direction.z));
    }

    private void DrawDebugMovementRays(Vector3 direction)
    {
#if UNITY_EDITOR
        if (!visualizeMoveInput) return;

        var lightGrey = new Color(0.75f, 0.75f, 0.75f, 0.8f);
        Debug.DrawRay(transform.position, Vector3.Cross(cameraPivot.transform.right, Vector3.up) * 2.5f, lightGrey.Adjust(2, 1));
        Debug.DrawRay(transform.position, cameraPivot.transform.right * 2.5f, lightGrey.Adjust(0, 1));

        Debug.DrawRay(transform.position, rb.velocity, Color.yellow.Adjust(3, 0.6f));
        Debug.DrawRay(transform.position, rb.velocity - Vector3.up * rb.velocity.y, Color.yellow);

        Debug.DrawRay(transform.position, direction, Color.white);
        UtilFunctions.DrawSphere(transform.position + direction, 0.15f, 6, 6, Color.white);
#endif
    }
}