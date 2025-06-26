using System.Collections.Generic;
using UnityEngine;

public class LocalMovementController : MonoBehaviour
{
	[Header("Movement Settings")]
	public float movementSpeed = 5f;

	//send position updates to the server 20 times per second.
	public float networkSendRate = 20f;

	[Header("Camera Follow")]
	public bool followLocalPlayer = true;

	[HideInInspector]
	public ModularNetworkManager networkManager;

	public Transform cameraTransform;
	public Vector3 cameraOffset = new Vector3(0, 8, -6);
	public float cameraFollowSpeed = 5f;

	[Header("Input Provider Settings")]
	public bool allowInputSwitching = true;

	private IInputProvider currentInputProvider;
	private KeyboardInputProvider keyboardInput;
	private VirtualJoystickProvider virtualJoystickInput;

	private Vector3 targetPosition;
	private Vector3 currentVelocity;
	// tracks when we last sent a network packet to the server.
	private float lastNetworkSendTime;

	// Client prediction - store movement history for server reconciliation
	private struct MovementState
	{
		public Vector3 position;
		public float timestamp;
		public Vector2 input;
	}

	private Queue<MovementState> movementHistory = new Queue<MovementState>();
	private const int maxHistorySize = 60;

	private void Start()
	{
		InitializeInputProviders();
		SetupCamera();

		targetPosition = transform.position;
		lastNetworkSendTime = Time.time;
	}

	private void InitializeInputProviders()
	{
		keyboardInput = new KeyboardInputProvider();
		keyboardInput.Initialize();

		virtualJoystickInput = FindObjectOfType<VirtualJoystickProvider>();
		if (virtualJoystickInput != null)
		{
			virtualJoystickInput.Initialize();
		}

#if UNITY_ANDROID || UNITY_IOS
            // Mobile: Virtual joystick only
            currentInputProvider = virtualJoystickInput;
            if (currentInputProvider == null)
            {
                Debug.LogWarning("No virtual joystick found for mobile platform!");
            }
#elif UNITY_STANDALONE

		currentInputProvider = keyboardInput;
#endif

		if (currentInputProvider == null)
		{
			Debug.LogError("No input provider available!");
		}
	}

	private void SetupCamera()
	{
		if (cameraTransform == null)
		{
			cameraTransform = Camera.main?.transform;
		}

		if (followLocalPlayer && cameraTransform != null)
		{
			cameraTransform.position = transform.position + cameraOffset;
			cameraTransform.LookAt(transform.position);
		}
	}

	private void Update()
	{
		HandleInputSwitching();
		HandleMovement();
		HandleCameraFollow();
		SendNetworkUpdates();
	}

	private void HandleInputSwitching()
	{
#if UNITY_STANDALONE
		if (allowInputSwitching)
		{
			if (Input.GetKeyDown(KeyCode.Tab))
			{
				if (currentInputProvider == keyboardInput && virtualJoystickInput != null)
				{
					currentInputProvider = virtualJoystickInput;
					Debug.Log("Switched to virtual joystick");
				}
				else if (currentInputProvider == (object)virtualJoystickInput)
				{
					currentInputProvider = keyboardInput;
					Debug.Log("Switched to keyboard");
				}
			}
		}
#endif
	}

	private void HandleMovement()
	{
		if (currentInputProvider == null) return;

		Vector2 input = currentInputProvider.GetMovementInput();

		if (input.magnitude > 0.1f)
		{
			Vector3 oldPosition = transform.position;
			Vector3 movement = new Vector3(input.x, 0, input.y) * movementSpeed * Time.deltaTime;
			targetPosition = transform.position + movement;

			transform.position = targetPosition;

			currentVelocity = (transform.position - oldPosition) / Time.deltaTime;

			MovementState state = new MovementState
			{
				position = transform.position,
				timestamp = Time.time,
				input = input
			};

			movementHistory.Enqueue(state);

			while (movementHistory.Count > maxHistorySize)
			{
				movementHistory.Dequeue();
			}
		}
		else
		{
			currentVelocity = Vector3.zero; // No movement, no velocity
		}
	}

	private void HandleCameraFollow()
	{
		if (followLocalPlayer && cameraTransform != null)
		{
			Vector3 targetCameraPosition = transform.position + cameraOffset;
			cameraTransform.position = Vector3.Lerp(
				cameraTransform.position,
				targetCameraPosition,
				cameraFollowSpeed * Time.deltaTime
			);
		}
	}

	private void SendNetworkUpdates()
	{
		if (Time.time - lastNetworkSendTime >= 1f / networkSendRate)
		{
			if (currentInputProvider != null && currentInputProvider.IsMoving())
			{
				var networkManager = FindObjectOfType<ModularNetworkManager>();
				if (networkManager != null && networkManager.IsConnected())
				{
					Vector3 velocity = currentVelocity;
					networkManager.SendPosition(transform.position, velocity);

					//Debug.Log($"Sent position: {transform.position}, velocity: {velocity}");
				}
			}
			lastNetworkSendTime = Time.time;
		}
	}

	// Called by NetworkMovementHandler for server reconciliation
	public void OnServerPositionReceived(Vector3 serverPosition, float serverTimestamp)
	{
		MovementState? matchingState = null;

		while (movementHistory.Count > 0)
		{
			MovementState state = movementHistory.Peek();
			if (state.timestamp <= serverTimestamp)
			{
				matchingState = state;
				movementHistory.Dequeue();
			}
			else
			{
				break;
			}
		}

		if (matchingState.HasValue)
		{
			float positionError = Vector3.Distance(matchingState.Value.position, serverPosition);

			if (positionError > 0.5f) 
			{
				Debug.Log($"Server reconciliation: correcting position by {positionError}");
				transform.position = serverPosition;
				targetPosition = serverPosition;
				movementHistory.Clear();
			}
		}
	}

	private void OnDestroy()
	{
		keyboardInput?.Cleanup();
		virtualJoystickInput?.Cleanup();
	}
}