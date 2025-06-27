using System;

public interface INetworkModule
{
	string ModuleId { get; }

	string ModuleName { get; }

	int Priority { get; }

	bool IsEnabled { get; }

	string[] Dependencies { get; }

	void Initialize();

	void Enable();

	void Disable();

	void Destroy();

	bool CanDisable();
}

public abstract class NetworkModuleBase : INetworkModule
{
	public abstract string ModuleId { get; }
	public abstract string ModuleName { get; }
	public virtual int Priority => 100; // Default priority for feature modules
	public bool IsEnabled { get; private set; }
	public virtual string[] Dependencies => new string[0]; // No dependencies by default

	private bool _isInitialized = false;

	public virtual void Initialize()
	{
		if (_isInitialized) return;

		UnityEngine.Debug.Log($"Initializing module: {ModuleName}");
		OnInitialize();
		_isInitialized = true;
	}

	public virtual void Enable()
	{
		if (!_isInitialized)
		{
			UnityEngine.Debug.LogError($"Cannot enable {ModuleName} - not initialized");
			return;
		}

		if (IsEnabled) return;

		UnityEngine.Debug.Log($"Enabling module: {ModuleName}");
		OnEnable();
		IsEnabled = true;
	}

	public virtual void Disable()
	{
		if (!IsEnabled) return;

		UnityEngine.Debug.Log($"Disabling module: {ModuleName}");
		OnDisable();
		IsEnabled = false;
	}

	public virtual void Destroy()
	{
		if (IsEnabled)
		{
			Disable();
		}

		if (_isInitialized)
		{
			UnityEngine.Debug.Log($"Destroying module: {ModuleName}");
			OnDestroy();
			_isInitialized = false;
		}
	}

	public virtual bool CanDisable()
	{
		return true;
	}

	protected abstract void OnInitialize();
	protected abstract void OnEnable();
	protected abstract void OnDisable();
	protected abstract void OnDestroy();
}