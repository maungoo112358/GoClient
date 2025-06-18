using Gamepacket;
using System.Collections.Generic;
using UnityEngine;

public class ModularNetworkManager : MonoBehaviour
{
	private List<INetworkModule> _modules = new List<INetworkModule>();
	private List<IUpdatableModule> _updatableModules = new List<IUpdatableModule>();
	private float[] _lastUpdateTimes;
	private GoClient _goClient;

	private void Awake()
	{
		_goClient = FindObjectOfType<GoClient>();
		_goClient.OnPacketReceived += HandleIncomingPacket;
	}

	private void Start()
	{
		_modules.AddRange(GetComponentsInChildren<INetworkModule>(true));
		_modules.Sort((a, b) => a.Priority.CompareTo(b.Priority));

		_lastUpdateTimes = new float[_modules.Count];

		for (int i = 0; i < _modules.Count; i++)
		{
			var module = _modules[i];

			module.Initialize();

			if (module is IPacketSender sender)
			{
				sender.OnPacketToSend += _goClient.SendPacket;
			}

			if (module is IUpdatableModule updatable)
			{
				_updatableModules.Add(updatable);
				_lastUpdateTimes[i] = 0f;
			}

			if (module.IsCore)
			{
				module.Enable();
			}
		}
	}

	private void Update()
	{
		float now = Time.time;

		for (int i = 0; i < _updatableModules.Count; i++)
		{
			var m = _updatableModules[i];
			float interval = m.UpdateInterval;
			if (interval <= 0f || now - _lastUpdateTimes[i] >= interval)
			{
				m.UpdateModule();
				_lastUpdateTimes[i] = now;
			}
		}
	}

	private void HandleIncomingPacket(GamePacket packet)
	{
		foreach (var module in _modules)
		{
			if (module.State == ModuleState.Enabled && module.CanHandle(packet))
			{
				module.HandlePacket(packet);
				break;
			}
		}
	}

	public bool EnableModule(string moduleName)
	{
		var module = _modules.Find(m => m.ModuleName == moduleName);
		if (module == null || module.State != ModuleState.Disabled || !module.CanEnable()) return false;
		foreach (var dep in module.Dependencies)
		{
			var depModule = _modules.Find(m => m.ModuleName == dep);
			if (depModule == null || depModule.State != ModuleState.Enabled) return false;
		}
		bool success = module.Enable();
		if (success) NotifyDependencyChange(module.ModuleName, true);
		return success;
	}

	public bool DisableModule(string moduleName)
	{
		var module = _modules.Find(m => m.ModuleName == moduleName);
		if (module == null || module.IsCore || module.State != ModuleState.Enabled || !module.CanDisable()) return false;
		bool success = module.Disable();
		if (success) NotifyDependencyChange(module.ModuleName, false);
		return success;
	}

	private void NotifyDependencyChange(string name, bool enabled)
	{
		foreach (var m in _modules)
		{
			if (m is IModuleAware aware)
			{
				if (enabled) aware.OnModuleEnabled(name);
				else aware.OnModuleDisabled(name);
			}
		}
	}
}
