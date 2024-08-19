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
    [SerializeField] float maxSpeed = 5;
    [SerializeField] float accModifier = 50;
    [Space(5)]
    [Tooltip("The point at which a difference in input is considered a change in direction. Used for aerial movement adjustments.")]
    [SerializeField] float dirChangeThreshold = 0.05f;
    [SerializeField] float minMoveBeforeRotate = 0.01f;

    [Header("Debug")]
    [SerializeField] [NaughtyAttributes.ReadOnly] Vector3 directionLastFrame;
#if UNITY_EDITOR
    [SerializeField] bool visualizeMoveInput;
#endif

    [Header("Rotation/Visuals")]
    [Tooltip("The object to use when determining what \"forward\" is, like the pivot point of the gameplay camera.")]
    [SerializeField] GameObject orienter;
    [Tooltip("All the objects that should rotate to face the direction of movement.")]
    [SerializeField] GameObject[] objectsToRotate;
    //[SerializeField] Animator anim;

    Rigidbody rb;

    public InputActionReference AbilityActionRef => abilityActionRef;
    public Vector3 Velocity => rb.velocity;



    void Start()
    {
        rb = GetComponent<Rigidbody>();

        moveActionRef.action.actionMap.Enable();
    }
    private void OnEnable()
    {
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        abilityActionRef.action.performed += OnAbilityUsed;
    }
    private void OnDisable()
    {
        rb.interpolation = RigidbodyInterpolation.None;
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
        direction = Quaternion.LookRotation(Vector3.Cross(orienter.transform.right, Vector3.up)) * direction.SwapAxes(1, 2);

        //anim.SetFloat("Walk Speed", direction.sqrMagnitude);

        bool movingEnoughToRotate = direction.sqrMagnitude > minMoveBeforeRotate * minMoveBeforeRotate;
        if (movingEnoughToRotate) RotateObjectsWithMovement(direction.normalized);

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



    private void RotateObjectsWithMovement(Vector3 direction)
    {
        if (objectsToRotate.Length < 1) return;

        //Make a rotation where direction is foward; ensure the y component of direction is 0.
        var desiredRotation = Quaternion.LookRotation(direction.Adjust(1, 0));
        foreach (var obj in objectsToRotate)
        {
            obj.transform.rotation = desiredRotation;
        }
    }

    private void DrawDebugMovementRays(Vector3 direction)
    {
#if UNITY_EDITOR
        if (!visualizeMoveInput) return;

        var lightGrey = new Color(0.75f, 0.75f, 0.75f, 0.8f);
        Debug.DrawRay(transform.position, Vector3.Cross(orienter.transform.right, Vector3.up) * 2.5f, lightGrey.Adjust(2, 1));
        Debug.DrawRay(transform.position, orienter.transform.right * 2.5f, lightGrey.Adjust(0, 1));

        Debug.DrawRay(transform.position, rb.velocity, Color.yellow.Adjust(3, 0.6f));
        Debug.DrawRay(transform.position, rb.velocity - Vector3.up * rb.velocity.y, Color.yellow);

        Debug.DrawRay(transform.position, direction, Color.white);
        UtilFunctions.DrawSphere(transform.position + direction, 0.15f, 6, 6, Color.white);
#endif
    }
}