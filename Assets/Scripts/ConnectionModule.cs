using Gamepacket;
using UnityEngine;
using System;

public class ConnectionModule : NetworkModuleBase,IUpdatableModule
{
	public override string ModuleName => "Connection";
	public override bool IsCore => true;
	public override ModulePriority Priority => ModulePriority.Critical;

	public event Action<string, string> OnConnected;
	public event Action<string> OnServerMessage;

	private string _privateId;
	private string _publicId;
	private float _lastHeartbeatAck;
	private float _heartbeatInterval = 5f;
	private float _nextHeartbeatTime;

	public override bool CanHandle(GamePacket packet)
	{
		return packet.HandshakeResponse != null || packet.HeartbeatAck != null || packet.ServerStatus != null;
	}

	public override void HandlePacket(GamePacket packet)
	{
		if (packet.HandshakeResponse != null)
		{
			_privateId = packet.HandshakeResponse.PrivateId;
			_publicId = packet.HandshakeResponse.PublicId;
			_lastHeartbeatAck = Time.time;
			_nextHeartbeatTime = Time.time + _heartbeatInterval;
			OnConnected?.Invoke(_privateId, _publicId);
			Debug.Log($"✅ Handshake success. Private: {_privateId}, Public: {_publicId}");
			return;
		}

		if (packet.HeartbeatAck != null)
		{
			_lastHeartbeatAck = Time.time;
			Debug.Log($"✅ Heartbeat ack: {packet.HeartbeatAck.ClientId}");
			return;
		}

		if (packet.ServerStatus != null)
		{
			OnServerMessage?.Invoke(packet.ServerStatus.Message);
			Debug.LogWarning($"⚠️ Server: {packet.ServerStatus.Message}");
		}
	}

	protected override bool DoEnable()
	{
		_lastHeartbeatAck = Time.time;
		_nextHeartbeatTime = Time.time + _heartbeatInterval;
		return true;
	}

	public  void UpdateModule()
	{
		if (Time.time >= _nextHeartbeatTime && !string.IsNullOrEmpty(_privateId))
		{
			var heartbeat = new GamePacket
			{
				Seq = 0,
				Heartbeat = new Heartbeat { ClientId = _privateId }
			};
			SendPacket(heartbeat);
			_nextHeartbeatTime = Time.time + _heartbeatInterval;
			Debug.Log("🔄 Heartbeat sent");
		}
	}

	public float UpdateInterval => 0;
}
