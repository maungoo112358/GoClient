using System;
using UnityEngine;
using Gamepacket;

// Module priority for initialization order
public enum ModulePriority
{
	Critical = 0,    // Core modules (Handshake, Lobby)
	High = 1,        // Essential modules (LocalMovement)
	Normal = 2,      // Standard modules (Chat, RemoteMovement)
	Low = 3,         // Optional modules (Building, Voice)
	Lowest = 4       // Visual/Effect modules (Particles, UI)
}

// Module state for runtime management
public enum ModuleState
{
	Disabled,
	Enabling,
	Enabled,
	Disabling,
	Error
}

// Base interface for all network modules
public interface INetworkModule
{
	// Module identity
	string ModuleName { get; }
	ModulePriority Priority { get; }
	ModuleState State { get; }

	// Dependencies
	string[] Dependencies { get; }  // Module names this depends on
	bool IsCore { get; }           // Cannot be disabled if true

	// Lifecycle methods
	bool Initialize();             // Setup module (called once)
	bool Enable();                 // Start module operations
	bool Disable();                // Stop module operations
	void Cleanup();                // Final cleanup (called once)

	// Runtime checks
	bool CanEnable();              // Check if safe to enable
	bool CanDisable();             // Check if safe to disable
	bool CanHandle(GamePacket packet);  // Check if module handles this packet

	// Packet processing
	void HandlePacket(GamePacket packet);  // Process incoming packet

	// Status
	string GetStatus();            // Get current module status for debugging
}

// Extended interface for modules that need regular updates
public interface IUpdatableModule : INetworkModule
{
	void UpdateModule();                 // Called every frame
	float UpdateInterval { get; }  // How often to update (0 = every frame)
}

// Extended interface for modules that send packets
public interface IPacketSender : INetworkModule
{
	// Event triggered when module wants to send a packet
	event Action<GamePacket> OnPacketToSend;
}

// Interface for modules that need to know about other modules
public interface IModuleAware : INetworkModule
{
	void OnModuleEnabled(string moduleName);
	void OnModuleDisabled(string moduleName);
}

// Base abstract class to reduce boilerplate
public abstract class NetworkModuleBase : MonoBehaviour, INetworkModule, IPacketSender
{
	[Header("Module Settings")]
	[SerializeField] protected bool _isCore = false;
	[SerializeField] protected ModulePriority _priority = ModulePriority.Normal;
	[SerializeField] protected string[] _dependencies = new string[0];

	// INetworkModule implementation
	public abstract string ModuleName { get; }
	public virtual ModulePriority Priority => _priority;
	public ModuleState State { get; protected set; } = ModuleState.Disabled;
	public virtual string[] Dependencies => _dependencies;
	public virtual bool IsCore => _isCore;

	// Events
	public event Action<GamePacket> OnPacketToSend;

	// Lifecycle - virtual methods with default behavior
	public virtual bool Initialize()
	{
		State = ModuleState.Disabled;
		Debug.Log($"🔧 {ModuleName} initialized");
		return true;
	}

	public virtual bool Enable()
	{
		if (State == ModuleState.Enabled) return true;

		State = ModuleState.Enabling;

		if (DoEnable())
		{
			State = ModuleState.Enabled;
			Debug.Log($"✅ {ModuleName} enabled");
			return true;
		}
		else
		{
			State = ModuleState.Error;
			Debug.LogError($"❌ {ModuleName} failed to enable");
			return false;
		}
	}

	public virtual bool Disable()
	{
		if (State == ModuleState.Disabled) return true;

		State = ModuleState.Disabling;

		if (DoDisable())
		{
			State = ModuleState.Disabled;
			Debug.Log($"⏹️ {ModuleName} disabled");
			return true;
		}
		else
		{
			State = ModuleState.Error;
			Debug.LogError($"❌ {ModuleName} failed to disable");
			return false;
		}
	}

	public virtual void Cleanup()
	{
		DoCleanup();
		Debug.Log($"🧹 {ModuleName} cleaned up");
	}

	// Runtime checks with sensible defaults
	public virtual bool CanEnable()
	{
		return State == ModuleState.Disabled;
	}

	public virtual bool CanDisable()
	{
		return State == ModuleState.Enabled && !IsCore;
	}

	// Abstract methods that subclasses must implement
	public abstract bool CanHandle(GamePacket packet);
	public abstract void HandlePacket(GamePacket packet);

	// Protected virtual methods for subclasses to override
	protected virtual bool DoEnable() { return true; }
	protected virtual bool DoDisable() { return true; }
	protected virtual void DoCleanup() { }

	// Utility method for sending packets
	protected void SendPacket(GamePacket packet)
	{
		if (State == ModuleState.Enabled)
		{
			OnPacketToSend?.Invoke(packet);
		}
		else
		{
			Debug.LogWarning($"⚠️ {ModuleName} tried to send packet while {State}");
		}
	}

	// Status for debugging
	public virtual string GetStatus()
	{
		return $"{ModuleName}: {State} (Core: {IsCore}, Priority: {Priority})";
	}

	// Unity lifecycle
	protected virtual void Start()
	{
		Initialize();
	}

	protected virtual void OnDestroy()
	{
		Cleanup();
	}
}

// Updatable module base class
public abstract class UpdatableModuleBase : NetworkModuleBase, IUpdatableModule
{
	[Header("Update Settings")]
	[SerializeField] protected float _updateInterval = 0f; // 0 = every frame

	private float _lastUpdateTime = 0f;

	public virtual float UpdateInterval => _updateInterval;

	// Abstract update method
	public abstract void UpdateModule();

	// Unity Update - handles timing
	private void Update()
	{
		if (State != ModuleState.Enabled) return;

		if (UpdateInterval <= 0f)
		{
			// Update every frame
			DoUpdate();
		}
		else if (Time.time - _lastUpdateTime >= UpdateInterval)
		{
			// Update at intervals
			DoUpdate();
			_lastUpdateTime = Time.time;
		}
	}

	// Call the abstract Update method
	private void DoUpdate()
	{
		try
		{
			Update();
		}
		catch (Exception ex)
		{
			Debug.LogError($"❌ {ModuleName} update error: {ex.Message}");
			State = ModuleState.Error;
		}
	}
}