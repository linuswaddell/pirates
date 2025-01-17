﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityStandardAssets.Characters.FirstPerson;

public class PlayerShipManager : NetworkBehaviour {

	private Ship currentlyControlledShip = null;
	private bool justActivatedWheel = false; // True for a single frame after the player takes control, to prevent deactivating during the same frame

	// Use this for initialization
	void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
		if (!isLocalPlayer || Player.localPlayer.isControllingShip == false)
		{
			return;
		}
		if (Input.GetKeyDown (KeyCode.E) && currentlyControlledShip != null && justActivatedWheel == false)
		{
			DeactivateShipWheel (currentlyControlledShip);
		}
		else if (Input.GetKeyDown (KeyCode.W) && currentlyControlledShip != null) 
		{
			CmdSetShipTravelling (currentlyControlledShip.GetComponent<NetworkIdentity> ().netId, true);
		}
		else if (Input.GetKeyDown (KeyCode.S) && currentlyControlledShip != null) 
		{
			CmdSetShipTravelling (currentlyControlledShip.GetComponent<NetworkIdentity> ().netId, false);
		}

		if (Input.GetKeyDown (KeyCode.A) && currentlyControlledShip != null) 
		{
			CmdSetShipRotatingLeft (currentlyControlledShip.GetComponent<NetworkIdentity> ().netId, true);
		}
		else if (Input.GetKeyDown (KeyCode.D) && currentlyControlledShip != null) 
		{
			CmdSetShipRotatingRight (currentlyControlledShip.GetComponent<NetworkIdentity> ().netId, true);
		}

		if (Input.GetKeyUp (KeyCode.A) && currentlyControlledShip != null)
		{
			CmdSetShipRotatingLeft (currentlyControlledShip.GetComponent<NetworkIdentity> ().netId, false);
		}
		if (Input.GetKeyUp (KeyCode.D) && currentlyControlledShip != null)
		{
			CmdSetShipRotatingRight (currentlyControlledShip.GetComponent<NetworkIdentity> ().netId, false);
		}

		if (justActivatedWheel)
		{
			justActivatedWheel = false;
		}
	}
		

	public void ActivateShipWheel (Ship ship) {
		if (!isLocalPlayer) {
			return;
		}
		if (ship.WheelIsOccupied) {
			Debug.Log ("Player tried to activate occupied ship wheel");
			return;
		}
		currentlyControlledShip = ship;
		justActivatedWheel = true;
		CmdSetShipWheelOccupied (ship.GetComponent<NetworkIdentity>().netId, true);
		GameObject wheelPlayerPosition = ship.GetComponentInChildren<WheelPlayerPosition> ().gameObject;
		Player.localPlayer.isControllingShip = true;
		Player.localPlayer.LockMovement ();
		transform.position = wheelPlayerPosition.transform.position;
		transform.rotation = wheelPlayerPosition.transform.rotation;
	}

	public void DeactivateShipWheel (Ship ship) {
		if (!isLocalPlayer) {
			return;
		}
		if (!ship.WheelIsOccupied) {
			Debug.LogWarning ("Player tried to deactivate a wheel that isn't occupied?");
			return;
		}
		NetworkInstanceId shipNetId = ship.GetComponent<NetworkIdentity> ().netId;
		currentlyControlledShip = null;
		CmdSetShipWheelOccupied (shipNetId, false);
		CmdSetShipRotatingLeft (shipNetId, false);
		CmdSetShipRotatingRight (shipNetId, false);
		Player.localPlayer.isControllingShip = false;
		Player.localPlayer.UnlockMovement ();
	}

	public void ParentPlayer (GameObject player, GameObject ship) {
		if (player == Player.localPlayer.gameObject) {
			NetworkInstanceId playerId = player.GetComponent<NetworkIdentity> ().netId;
			NetworkInstanceId shipId = ship.GetComponent<NetworkIdentity> ().netId;
			CmdParentPlayer (playerId, shipId);
		}
	}

	public void UnparentPlayer (GameObject player) {
		if (player == Player.localPlayer.gameObject) {
			NetworkInstanceId playerId = player.GetComponent<NetworkIdentity> ().netId;
			CmdUnparentPlayer (playerId);
		}
	}

	[Command]
	void CmdSetShipTravelling (NetworkInstanceId shipNetId, bool isTravelling)
	{
		Ship ship = NetworkServer.FindLocalObject (shipNetId).GetComponent<Ship>();
		ship.isTraveling = isTravelling;
		Debug.Log ("hey");
	}

	[Command]
	void CmdSetShipRotatingRight (NetworkInstanceId shipNetId, bool isRotating)
	{
		Ship ship = NetworkServer.FindLocalObject (shipNetId).GetComponent<Ship>();
		ship.isRotatingRight = isRotating;
	}

	[Command]
	void CmdSetShipRotatingLeft (NetworkInstanceId shipNetId, bool isRotating)
	{
		Ship ship = NetworkServer.FindLocalObject (shipNetId).GetComponent<Ship>();
		ship.isRotatingLeft = isRotating;
	}


	[Command]
	void CmdSetShipWheelOccupied (NetworkInstanceId shipNetId, bool isOccupied) 
	{
		NetworkServer.FindLocalObject(shipNetId).GetComponent<Ship>().SetWheelOccupied (isOccupied);
	}

	[Command]
	void CmdParentPlayer (NetworkInstanceId playerNetId, NetworkInstanceId shipId) {
		Debug.Log("[SERVER] Command to parent player to a boat has been called");
		RpcParentPlayer (playerNetId, shipId);
	}

	[ClientRpc]
	void RpcParentPlayer (NetworkInstanceId playerNetId, NetworkInstanceId shipId) {
		GameObject player = ClientScene.FindLocalObject (playerNetId);
		GameObject ship = ClientScene.FindLocalObject (shipId);

		if (Player.localPlayer.gameObject == player) {
			GetComponent<CustomFirstPersonController> ().RecordGlobalRotation ();
		}


		player.transform.SetParent (ship.transform); 


		//player.GetComponent<NetworkTransform> ().sendInterval = 0.06f; // Lower send rate while on a ship to eliminate jitter
		if (Player.localPlayer.gameObject == player) {
			GetComponent<CustomFirstPersonController> ().RestoreGlobalRotation ();
		}
	}
		

	[Command]
	void CmdUnparentPlayer (NetworkInstanceId playerNetId) {
		RpcUnparentPlayer (playerNetId);
	}

	[ClientRpc]
	void RpcUnparentPlayer (NetworkInstanceId playerNetId) {
		GameObject player = ClientScene.FindLocalObject (playerNetId);
		if (Player.localPlayer.gameObject == player) {
			GetComponent<CustomFirstPersonController> ().RecordGlobalRotation ();
		}


		player.transform.parent = null;


		//player.GetComponent<NetworkTransform> ().sendInterval = 0.1f; // Reset send rate when back on land
		if (Player.localPlayer.gameObject == player) {
			GetComponent<CustomFirstPersonController> ().RestoreGlobalRotation ();
		}
	}
}
