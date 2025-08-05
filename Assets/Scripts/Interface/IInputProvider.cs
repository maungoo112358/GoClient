using UnityEngine;

public interface IInputProvider
{
	Vector2 GetMovementInput();

	bool IsMoving();

	void Initialize();
	
	void Cleanup();
}