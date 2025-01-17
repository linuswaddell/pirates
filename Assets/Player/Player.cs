﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using UnityStandardAssets.Characters.FirstPerson;
using UnityStandardAssets.CrossPlatformInput;

public class Player : NetworkBehaviour {

	public static Player localPlayer;

	private GameObject playerMesh;
	private GameObject playerHands;
	private GameObject thirdPersonRightHand;
	private Camera camera;
	private AudioListener audioListener;
	private CustomFirstPersonController firstPersonController;
	private UIManager uiManager;
	private PlayerInventory inventory;
	private PlayerAnimationController animationController;
	private PlayerShipManager shipManager;

	private int? activeHotbarSlot = null;
	private bool isItemEquipped = false;
	[SyncVar(hook="CheckIfDead")] private bool isDead = false;

	public bool isControllingShip = false;
	public bool IsItemEquipped {get{return isItemEquipped;}}
	[SyncVar] public string playerID; // Super early placeholder stuff. In the future we might store the CSteamID variable instead, or something else.

	public bool activateDeath; // For testing purposes only; allows activating death from inspector

	public override void OnStartClient()
	{
		CheckIfDead (isDead); // Make sure players that are already dead are displayed as such when a client joins
		                      // Trust me, this is the place for this.
	}

	public override void OnStartLocalPlayer()
	{
		localPlayer = this;

		CmdSetPlayerID (Network.player.ipAddress);

		inventory = GetComponent<PlayerInventory> ();

		animationController = GetComponent<PlayerAnimationController> ();

		shipManager = GetComponent<PlayerShipManager> ();

		uiManager = GameObject.FindObjectOfType<UIManager> ();
		if (!uiManager)
			Debug.LogException (new System.Exception("No UIManager object found in scene"));

		playerMesh = transform.Find ("Player Model/Mesh").gameObject;  // Set mesh to "Local Player Mesh" layer, which is culled by player's camera
		playerMesh.layer = 8;                                          // ...so the player doesn't see his own body

		playerHands = transform.Find ("Camera/Player Hands").gameObject;
		playerHands.SetActive (true);

		foreach (Transform child in transform.Find("Player Model").GetComponentsInChildren<Transform>())
		{
			if (child.tag == "Right Hand Third Person")
			{
				thirdPersonRightHand = child.gameObject;
				break;
			}
		}

		foreach (Transform child in thirdPersonRightHand.GetComponentsInChildren<Transform>())
		{
			child.gameObject.layer = 8;
		}

		// Enable camera and controller components for just the local instance

		camera = GetComponentInChildren<Camera> ();
		camera.enabled = true;

		Debug.Log (camera);

		audioListener = GetComponentInChildren<AudioListener> ();
		audioListener.enabled = true;

		firstPersonController = GetComponent<CustomFirstPersonController> ();
		firstPersonController.enabled = true;


		uiManager.UpdateInventoryPanelsAfterSync();
		uiManager.EnableCursorLock ();

		GameObject.FindObjectOfType<Canvas> ().worldCamera = camera;
		GameObject.FindObjectOfType<Canvas> ().planeDistance = 0.1f;

		DebugID ();

		CmdUpdateServerManagerPlayerList ();

	}

	void Update()
	{
		if (!isLocalPlayer)
			return;

		// For testing purposes only; allows activating death from inspector
		if (activateDeath) {
			Die ();
			activateDeath = false;
		}


		if (Input.GetKeyDown (KeyCode.F)) 
		{
			CmdDie ();
		}

		if (Input.GetKeyDown (KeyCode.Tab))
		{
			if (uiManager.IsInventoryOpen())
			{
				uiManager.DisableInventoryScreen();
			}
			else
			{
				uiManager.EnableInventoryScreen ();
			}
		}

		for (int i = 1; i <= 6; i++)
		{
			if (Input.GetKeyDown(i.ToString()))
			{
				if (i - 1 == activeHotbarSlot)
					DeactivateHotbarSlot ();
				else 
				{
					ActivateHotbarSlot (i - 1);
				}
			}
		}

		if (Input.GetMouseButtonDown(1) && !isControllingShip)
		{
			if (IsItemEquipped && animationController.CurrentEquippedItemID != 0) 
			{
				animationController.SetAiming (true, ItemManager.Dictionary.GetItem(animationController.CurrentEquippedItemID).AimFovMultiplier);
			}
		}
		else if (Input.GetMouseButtonUp(1))
		{
			animationController.SetAiming (false);
		}

		if (Input.GetMouseButtonDown(0) && !isControllingShip)
		{
			if (IsItemEquipped && animationController.CurrentEquippedItemID != 0) 
			{
				animationController.ActivateAttack();
			}
		}
	}

	void FixedUpdate()
	{
		if (!isLocalPlayer)
			return;

		if (!isControllingShip) {
			CastRay ();
		} else {
			uiManager.ClearInteractText ();
		}
	}
		

	void CastRay ()
	{
		RaycastHit hit;

		if (Physics.Raycast (camera.transform.position, camera.transform.forward, out hit, 2.4f, 1, QueryTriggerInteraction.Collide))
		{
			Item item = hit.collider.GetComponent<Item> ();
			ShipWheel shipWheel = hit.collider.GetComponent <ShipWheel> ();
			Harbourmaster harbourmaster = hit.collider.GetComponent<Harbourmaster> ();
			if (item)
			{
				uiManager.SetInteractTextToItem (item);
				if (Input.GetKeyDown(KeyCode.E))
				{
					AttemptPickUpItem (item);
				}
			}
			else if (shipWheel) 
			{
				uiManager.SetInteractText ("E - Take control");
				if (Input.GetKeyDown(KeyCode.E))
				{
					shipManager.ActivateShipWheel (shipWheel.GetComponentInParent<Ship>());
				}
			}
			else if (harbourmaster) {
				uiManager.SetInteractText ("E - Access port");
				if (Input.GetKeyDown(KeyCode.E))
				{
					uiManager.EnablePortMenu (harbourmaster.GetPort());
				}
			}
			else 
			{
				uiManager.ClearInteractText ();
			}
		}
		else
		{
			uiManager.ClearInteractText ();
		}
	}

	void AttemptPickUpItem (Item item)
	{
		int slotX, slotY;
		if (inventory.AddItemToInventory(item.ItemID, out slotX, out slotY))
		{
			CmdDestroyObject (item.gameObject);
			uiManager.UpdateInventoryPanelsAfterSync ();
		}
	}

	void ActivateHotbarSlot (int slot)
	{
		uiManager.SetActiveHotbarSlot (slot);
		activeHotbarSlot = slot;

		EquipItem (inventory.GetInventoryArray () [slot, 0]); // Doesn't sync; might be a problem
	}

	void DeactivateHotbarSlot ()
	{
		activeHotbarSlot = null;
		uiManager.ClearActiveHotbarSlot ();
		UnequipItem ();
	}
		
	void CheckIfDead (bool isDead) // Called locally from each gameobject after isDead for any object has been updated
	{
		Debug.Log ("isDead has been changed");
		if (isDead)
		{
			Debug.Log ("A player is dead.");
			GetComponent<Animator>().enabled = false;
			GetComponent<CustomFirstPersonController>().enabled = false;
			GetComponent<NetworkTransform> ().enabled = false;

			foreach (Rigidbody rigidbody in GetComponentsInChildren<Rigidbody>())
			{
				if (rigidbody.tag != "Player")
				rigidbody.isKinematic = false;
			}

			gameObject.layer = 10; // Ragdoll layer, to disable collisions with players
		}
	}

	void DebugID ()
	{
		Debug.Log(playerID);
	}
		
	void EquipItem (int itemID)
	{
		isItemEquipped = true;
		animationController.SetEquippedItem (itemID);
		animationController.ActivateHands ();
	}
		
	void UnequipItem ()
	{
		isItemEquipped = false;
		animationController.DeactivateHands();
	}

	public void Die() // Public wrapper for command
	{
		Debug.Log ("Die() called on " + name);

		CmdDie ();

	}

	public void DropItem (int slotX, int slotY) {
		int id = inventory.GetInventoryArray ()[slotX, slotY];
		inventory.RemoveItemFromInventory (slotX, slotY);
		UpdateItemInCurrentHotbarSlot ();
		uiManager.UpdateInventoryPanelsAfterSync ();
		CmdDropItem (id, gameObject.GetComponent<NetworkIdentity>().netId);
	}

	public void UpdateItemInCurrentHotbarSlot ()
	{
		if (activeHotbarSlot != null)
			EquipItem (inventory.GetInventoryArray ()[(int)activeHotbarSlot, 0]);
	}

	public void LockMovement ()
	{
		if (!isLocalPlayer) {
			return;
		}
		firstPersonController.LockMovement ();
	}

	public void UnlockMovement ()
	{
		if (!isLocalPlayer) {
			return;
		}
		firstPersonController.UnlockMovement ();
	}

	public Camera GetCamera ()
	{
		return camera;
	}



	[Command]
	void CmdUpdateServerManagerPlayerList() {
		ServerManager.instance.UpdatePlayerList ();
	}

	[Command]
	void CmdSetPlayerID(string id)
	{
		playerID = id;
	}

	[Command]
	void CmdDestroyObject(GameObject objectToDestroy)
	{
		NetworkServer.Destroy(objectToDestroy);
		Debug.Log ("[SERVER] Destroy command called on " + objectToDestroy);
	}

	[Command]
	void CmdDie ()
	{
		Debug.Log ("[SERVER] CmdDie called");
		isDead = true;
	}

	[Command]
	void CmdDropItem (int itemID, NetworkInstanceId playerID) {
		GameObject cameraToAlignWith = NetworkServer.FindLocalObject(playerID).GetComponent<Player>().GetComponentInChildren<Camera>().gameObject;
		GameObject itemToDrop = Instantiate (ItemManager.Dictionary.GetItemObject (itemID), cameraToAlignWith.transform.position + (cameraToAlignWith.transform.forward * 0.2f), cameraToAlignWith.transform.rotation);
		itemToDrop.GetComponent<Rigidbody> ().velocity = cameraToAlignWith.transform.forward * 2f;
		NetworkServer.Spawn (itemToDrop);
	}
}
