using UnityEngine;

public class KeyboardInputProvider : IInputProvider
{
	public Vector2 GetMovementInput()
	{
		Vector2 input = Vector2.zero;

		if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
			input.x -= 1f;
		if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
			input.x += 1f;

		if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
			input.y += 1f;
		if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
			input.y -= 1f;

		// Normalize diagonal input to prevent faster movement
		if (input.magnitude > 1f)
			input.Normalize();

		return input;
	}

	public bool IsMoving()
	{
		return GetMovementInput().magnitude > 0f;
	}

	public void Initialize()
	{
		Debug.Log("Keyboard input provider initialized");
	}

	public void Cleanup()
	{
		Debug.Log("Keyboard input provider cleaned up");
	}
}