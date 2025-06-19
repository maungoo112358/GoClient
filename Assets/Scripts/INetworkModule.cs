using System;

/// <summary>
/// Interface for all optional network modules
/// Defines the lifecycle and behavior contract for modular features
/// </summary>
public interface INetworkModule
{
	/// <summary>
	/// Unique identifier for this module
	/// </summary>
	string ModuleId { get; }

	/// <summary>
	/// Human-readable name for this module
	/// </summary>
	string ModuleName { get; }

	/// <summary>
	/// Priority for initialization order (lower numbers = higher priority)
	/// Core modules: 0-99, Feature modules: 100+
	/// </summary>
	int Priority { get; }

	/// <summary>
	/// Whether this module is currently enabled
	/// </summary>
	bool IsEnabled { get; }

	/// <summary>
	/// Modules this one depends on (must be loaded first)
	/// </summary>
	string[] Dependencies { get; }

	/// <summary>
	/// Initialize the module (called once when module is first loaded)
	/// </summary>
	void Initialize();

	/// <summary>
	/// Enable/activate the module (can be called multiple times)
	/// </summary>
	void Enable();

	/// <summary>
	/// Disable/deactivate the module (can be called multiple times)
	/// </summary>
	void Disable();

	/// <summary>
	/// Clean up and destroy the module (called once when unloading)
	/// </summary>
	void Destroy();

	/// <summary>
	/// Check if this module can be safely disabled right now
	/// </summary>
	bool CanDisable();
}

/// <summary>
/// Base implementation with common functionality
/// Optional modules can inherit from this instead of implementing INetworkModule directly
/// </summary>
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
		return true; // Most modules can be disabled safely
	}

	// Abstract methods for subclasses to implement
	protected abstract void OnInitialize();
	protected abstract void OnEnable();
	protected abstract void OnDisable();
	protected abstract void OnDestroy();
}