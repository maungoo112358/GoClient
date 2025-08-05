using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ModuleRegistry
{
	private Dictionary<string, INetworkModule> _modules = new Dictionary<string, INetworkModule>();
	private List<INetworkModule> _initializationOrder = new List<INetworkModule>();
	private bool _isInitialized = false;

	public bool RegisterModule(INetworkModule module)
	{
		if (module == null)
		{
			Debug.LogError("Cannot register null module");
			return false;
		}

		if (_modules.ContainsKey(module.ModuleId))
		{
			Debug.LogError($"Module {module.ModuleId} is already registered");
			return false;
		}

		_modules[module.ModuleId] = module;
		Debug.Log($"Registered module: {module.ModuleName} (ID: {module.ModuleId})");

		RebuildInitializationOrder();
		return true;
	}

	public bool UnregisterModule(string moduleId)
	{
		if (!_modules.ContainsKey(moduleId))
		{
			Debug.LogWarning($"Module {moduleId} not found for unregistration");
			return false;
		}

		var module = _modules[moduleId];

		var dependents = GetDependentModules(moduleId);
		if (dependents.Count > 0)
		{
			Debug.LogError($"Cannot unregister {moduleId} - other modules depend on it: {string.Join(", ", dependents.Select(m => m.ModuleId))}");
			return false;
		}

		module.Destroy();
		_modules.Remove(moduleId);
		RebuildInitializationOrder();

		Debug.Log($"Unregistered module: {module.ModuleName}");
		return true;
	}

	public void InitializeAllModules()
	{
		if (_isInitialized)
		{
			Debug.LogWarning("Modules already initialized");
			return;
		}

		Debug.Log("Initializing all modules...");

		foreach (var module in _initializationOrder)
		{
			try
			{
				module.Initialize();
			}
			catch (Exception ex)
			{
				Debug.LogError($"Failed to initialize module {module.ModuleName}: {ex.Message}");
			}
		}

		_isInitialized = true;
		Debug.Log("All modules initialized");
	}

	public void EnableAllModules()
	{
		Debug.Log("Enabling all modules...");

		foreach (var module in _initializationOrder)
		{
			try
			{
				module.Enable();
			}
			catch (Exception ex)
			{
				Debug.LogError($"Failed to enable module {module.ModuleName}: {ex.Message}");
			}
		}
	}

	public void DisableAllModules()
	{
		Debug.Log("Disabling all modules...");

		// Disable in reverse order
		for (int i = _initializationOrder.Count - 1; i >= 0; i--)
		{
			try
			{
				_initializationOrder[i].Disable();
			}
			catch (Exception ex)
			{
				Debug.LogError($"Failed to disable module {_initializationOrder[i].ModuleName}: {ex.Message}");
			}
		}
	}

	public bool EnableModule(string moduleId)
	{
		if (!_modules.TryGetValue(moduleId, out var module))
		{
			Debug.LogError($"Module {moduleId} not found");
			return false;
		}

		if (!CheckDependenciesEnabled(module))
		{
			Debug.LogError($"Cannot enable {moduleId} - dependencies not met");
			return false;
		}

		try
		{
			module.Enable();
			return true;
		}
		catch (Exception ex)
		{
			Debug.LogError($"Failed to enable module {module.ModuleName}: {ex.Message}");
			return false;
		}
	}

	public bool DisableModule(string moduleId)
	{
		if (!_modules.TryGetValue(moduleId, out var module))
		{
			Debug.LogError($"Module {moduleId} not found");
			return false;
		}

		if (!module.CanDisable())
		{
			Debug.LogWarning($"Module {moduleId} cannot be safely disabled right now");
			return false;
		}

		// Check if other enabled modules depend on this one
		var enabledDependents = GetDependentModules(moduleId).Where(m => m.IsEnabled).ToList();
		if (enabledDependents.Count > 0)
		{
			Debug.LogError($"Cannot disable {moduleId} - enabled modules depend on it: {string.Join(", ", enabledDependents.Select(m => m.ModuleId))}");
			return false;
		}

		try
		{
			module.Disable();
			return true;
		}
		catch (Exception ex)
		{
			Debug.LogError($"Failed to disable module {module.ModuleName}: {ex.Message}");
			return false;
		}
	}

	public INetworkModule GetModule(string moduleId)
	{
		_modules.TryGetValue(moduleId, out var module);
		return module;
	}

	public IEnumerable<INetworkModule> GetAllModules()
	{
		return _modules.Values;
	}

	public IEnumerable<INetworkModule> GetEnabledModules()
	{
		return _modules.Values.Where(m => m.IsEnabled);
	}

	public bool IsModuleRegistered(string moduleId)
	{
		return _modules.ContainsKey(moduleId);
	}

	public void DestroyAllModules()
	{
		Debug.Log("Destroying all modules...");

		// Destroy in reverse order
		for (int i = _initializationOrder.Count - 1; i >= 0; i--)
		{
			try
			{
				_initializationOrder[i].Destroy();
			}
			catch (Exception ex)
			{
				Debug.LogError($"Failed to destroy module {_initializationOrder[i].ModuleName}: {ex.Message}");
			}
		}

		_modules.Clear();
		_initializationOrder.Clear();
		_isInitialized = false;
	}

	private void RebuildInitializationOrder()
	{
		_initializationOrder.Clear();

		// Sort by priority first, then resolve dependencies
		var sortedModules = _modules.Values.OrderBy(m => m.Priority).ToList();

		foreach (var module in sortedModules)
		{
			AddModuleInDependencyOrder(module);
		}
	}

	private void AddModuleInDependencyOrder(INetworkModule module)
	{
		if (_initializationOrder.Contains(module))
			return;

		// Add dependencies first
		foreach (var depId in module.Dependencies)
		{
			if (_modules.TryGetValue(depId, out var dependency))
			{
				AddModuleInDependencyOrder(dependency);
			}
			else
			{
				Debug.LogError($"Module {module.ModuleId} depends on {depId} which is not registered");
			}
		}

		_initializationOrder.Add(module);
	}

	private bool CheckDependenciesEnabled(INetworkModule module)
	{
		foreach (var depId in module.Dependencies)
		{
			if (_modules.TryGetValue(depId, out var dependency))
			{
				if (!dependency.IsEnabled)
				{
					Debug.LogError($"Dependency {depId} is not enabled");
					return false;
				}
			}
			else
			{
				Debug.LogError($"Dependency {depId} is not registered");
				return false;
			}
		}
		return true;
	}

	private List<INetworkModule> GetDependentModules(string moduleId)
	{
		return _modules.Values.Where(m => m.Dependencies.Contains(moduleId)).ToList();
	}
}