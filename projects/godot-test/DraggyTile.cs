using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

//clean up snapping?
//move offscreen tiles back on screen
//push tiles apart on break
//screenshake

public class DraggyTile : Tile {
//font by Paul D. Hunt from https://fonts.google.com/specimen/Source+Sans+Pro
	Vector3 offset;
	float snapStrength = 0.2f;
	float snapStickiness = 0.6f;
	//links to any adjacent tiles that have been snapped to
	DraggyTile[] adjacent;
	//adjacent column/row dimensions (to prevent tiles from snapping when another tile with different dimensions exists in the same row/column
	//LinkedList<Expression> columns, rows;
	//Expression[] adjacentDim;
	//all the current tiles (used so we don't have to find them each time)
	DraggyTile[] allTiles;
	//set of tiles created when we're checking for snaps etc.
	HashSet<DraggyTile> tileSet;
	public Vector2 coords;
	//static int north = 0;
	//static int east = 1;
	//static int south = 2;
	//static int west = 3;
	static string[,] tagPairs = {{"north", "south"}, {"east", "west"}, {"south", "north"}, {"west", "east"}};
	//stores tile being snapped to until it's ready to be linked as an adjacent tile
	DraggyTile snapped;
	//stores tile with the shortest snap distance
	DraggyTile dragSnapped;
	int snapDir;
	Vector3 snapMovement;
	//prevent looping through adjacent tiles
	bool alreadyFlagged;
	//timer for clearing adjacents before dragging; if pointer is held down before dragging, adjacents are cleared
	float timer, rotateTimer;
	float rotateTime = 0.4f;
	float clearTime = 0.5f;
	float menuTime = 0.7f;
	bool beingHeld;
	GameObject radialMenu;
	delegate int GetRadialDelegate();
	delegate bool BoolRadialDelegate();
	delegate void SetRadialDelegate(int i);
	delegate void SetRadialMenuDelegate(int i, string[] desc, bool[] en);
	delegate void CloseRadialDelegate(bool clear, bool instant);
	delegate void UpdateRadialCursorDelegate(Vector3 v);
	GetRadialDelegate getRadialSelected;
	BoolRadialDelegate radialInitialized;
	SetRadialDelegate setRadialSelected;
	CloseRadialDelegate closeRadialMenu;
	UpdateRadialCursorDelegate updateRadialCursor;
	SetRadialMenuDelegate setRadialMenu;
	
	delegate void ShakeScreenDelegate(int m);
	ShakeScreenDelegate shakeScreen;
	bool isShakeable;
	
	private float screenRight;
	private float screenLeft = 0f;
	private float screenTop;
	private float screenBot = 0f;
	
	Vector3 mouseLocation;
	Ray ray;
	
	ExpressionBlurb expressionBlurb;
	
	TextMesh debugBlurb;

    public override void Start() {
		base.Start();
		SetDelegates();
		snapped = null;
		snapDir = -1;
		adjacent = new DraggyTile[4];
		dragSnapped = null;
		alreadyFlagged = false;
		radialMenu = transform.GetChild(0).gameObject;
		getRadialSelected = radialMenu.GetComponent<RadialMenu>().GetSelected;
		radialInitialized = radialMenu.GetComponent<RadialMenu>().Initialized;
		setRadialSelected = radialMenu.GetComponent<RadialMenu>().SetSelected;
		setRadialMenu = radialMenu.GetComponent<RadialMenu>().SetMenu;
		closeRadialMenu = radialMenu.GetComponent<RadialMenu>().CloseMenu;
		updateRadialCursor = radialMenu.GetComponent<RadialMenu>().UpdateCursor;
		setRadialSelected(-1);
		
		expressionBlurb = GameObject.Find("ExpressionBulletin").GetComponent<ExpressionBlurb>();
		//debugBlurb = GameObject.Find("debug").GetComponent<TextMesh>();
		
		shakeScreen = GameObject.Find("ButtonBar").GetComponent<ScreenShaker>().ScreenShake;
		isShakeable = true;
		
		mouseLocation = new Vector3();
		ray = new Ray();
		
		screenRight = GameObject.Find("Container").GetComponent<TileContainer>().GetScreenWidthInUnits();
		screenTop = GameObject.Find("Container").GetComponent<TileContainer>().GetScreenHeightInUnits();
		
//if (Application.isMobilePlatform) {
//	clearTime = 1.5f;
//	menuTime = 3f;
//	rotateTime = 1.4f;
//}
    }
	
	//if a tile is being held, update counter and open menu if need be
	//if the radial menu is closed and something was selected, do the selected thing
	public override void Update() {
//debugBlurb.text = Camera.main.ScreenToWorldPoint(Input.mousePosition).ToString();
		base.Update();
		if (beingHeld) {
			timer += Time.deltaTime;
		}
		if (timer > menuTime) {
			timer = 0;
			beingHeld = false;
			
			//set radial menu stuff
			string[] segmentDescription = new string[6];
			segmentDescription[0] = "move";
			segmentDescription[1] = "delete tile";
			segmentDescription[2] = "delete group";
			segmentDescription[3] = "break away";
			segmentDescription[4] = "rotate tile";
			segmentDescription[5] = "multiply by -1";
			bool[] segmentEnabled = new bool[6];
			for (int i = 0; i < 6; i++)
				segmentEnabled[i] = true;
			if (HasAdjacents()) {
				segmentEnabled[4] = false;
				segmentEnabled[5] = false;
			}
			setRadialMenu(6, segmentDescription, segmentEnabled);
			radialMenu.SetActive(true);
		}
		
		if (radialInitialized() && !radialMenu.activeSelf) {
			if (getRadialSelected() == 1) {
				ClearAdjacents();
				TrashTilesRecursively(this);
			}
			else if (getRadialSelected() == 2) {
				shakeScreen(2);
				TrashTilesRecursively(this);
			}
			else if (getRadialSelected() == 3) {
				ClearAdjacents();
			}
			else if (getRadialSelected() == 4) {
				RotateTile();
			}
			else if (getRadialSelected() == 5  && !HasAdjacents()) {
				length = length.MakeNegative();
				expressionBlurb.QueueUpdate();
				UpdateLabelsText();
			}
			setRadialSelected(-1);
			closeRadialMenu(true, true);
		}
		
		if (rotateTimer < rotateTime)
			rotateTimer += Time.deltaTime;
		
		
/*if (Application.isMobilePlatform) {
		if (Input.touchCount > 0) {
			
			Touch touch = Input.GetTouch(0);
			
			//Create a ray going from the camera through the mouse position
			ray = Camera.main.ScreenPointToRay(touch.localPosition);
			mouseLocation = Camera.main.transform.localPosition + ray.direction * Vector3.Distance(touch.localPosition, Camera.main.transform.localPosition);
			
				switch (touch.phase)
				{
					case TouchPhase.Began:
						RaycastHit hit;
						if (Physics.Raycast(ray, out hit, 100f) && hit.collider.gameObject.GetComponent<DraggyTile>() == this) {
							touchDrag = true;
							OnBeginDragDelegate(null);
						}
						break;
						
					case TouchPhase.Moved:
						if (touchDrag)
							OnDragDelegate(null);
						break;

					case TouchPhase.Ended:
						if (touchDrag) {
							OnEndDragDelegate(null);
							touchDrag = false;
						}
						break;
				}
		}
}
else {*/
		//Create a ray going from the camera through the mouse position
		ray = Camera.main.ScreenPointToRay(Input.mousePosition);
		//mouseLocation = Camera.main.transform.localPosition + ray.direction * Vector3.Distance(transform.localPosition, Camera.main.transform.localPosition);
		
		mouseLocation = GameObject.Find("Container").GetComponent<TileContainer>().GetMouseLocation(); //Camera.main.ScreenToWorldPoint(Input.mousePosition);
		//mouseLocation = transform.InverseTransformPoint(mouseLocation);
		mouseLocation = new Vector3(mouseLocation.x, mouseLocation.y, 0f);
		
Debug.DrawRay(transform.parent.TransformPoint(mouseLocation), new Vector3(0,0,7), Color.yellow);
//Debug.Log(mouseLocation);
					
		updateRadialCursor(mouseLocation);
	}
	
	//grabs a list of all the tiles so that we don't have to constantly find them
	//check if we've been holding this tile before dragging, store the offset between the pointer and the tile
	//set layers of dragged tile(s) to ignore raycasts
    public void OnBeginDragDelegate(PointerEventData data) {
		
		allTiles = FindObjectsOfType<DraggyTile>();
			
if (data.delta.magnitude > 2) {

		//Debug.Log(allTiles.Length);
		
		OnDragAfterHold();
		/*//get mouse location (to transform tiles by the same offset on move)
		Ray ray = Camera.main.ScreenPointToRay(data.localPosition);
		Vector3 mouseLocation = Camera.main.transform.localPosition + ray.direction * Vector3.Distance(transform.localPosition, Camera.main.transform.localPosition);*/
		
}
	}
	
	//sets layers when dragging so that raycasts can pass through
	public static void ChangeLayersRecursively(DraggyTile tile, int layer) {
		ChangeLayers(tile.gameObject, layer);
		
		for (int dir = 0; dir < 4; dir++) {
			if (tile.adjacent[dir] != null && tile.adjacent[dir].gameObject.layer != layer)	
				ChangeLayersRecursively(tile.adjacent[dir], layer);
		}
	}
	
	//if we start dragging after holding the tile, break it away from adjacent tiles
	void OnDragAfterHold() {
		if (beingHeld && timer > clearTime) {
			ClearAdjacents();
		}
		timer = 0f;
//debugBlurb.text = "OnDragAfterHold";
		rotateTimer = rotateTime + 1f;
		beingHeld = false;
		
		//offset between transform position and mouse position
		//offset = Camera.main.transform.localPosition + ray.direction * Vector3.Distance(transform.localPosition, Camera.main.transform.localPosition) - transform.localPosition;
		offset = mouseLocation - transform.localPosition;

		//let raycasts pass through
		//if(!radialMenu.activeSelf)
		ChangeLayersRecursively(this, 2);
	}
	
	//while dragging, check for any snaps; if none, move tile(s)
	public void OnDragDelegate(PointerEventData data) {
		//in case we missed setting layers on begin drag
		//let raycasts pass through
		//if (data.pointerDrag.layer != 2 && data.pointerDrag.GetComponent<DraggyTile>().getRadialSelected() == -1)
		//	ChangeLayersRecursively(this.gameObject, 2);
	
		/*
		//Create a ray going from the camera through the mouse position
		Ray ray = Camera.main.ScreenPointToRay(data.localPosition);
		Vector3 mouseLocation = Camera.main.transform.localPosition + ray.direction * Vector3.Distance(transform.localPosition, Camera.main.transform.localPosition);*/
		
		if (beingHeld && data.delta.magnitude > 2) {
			OnDragAfterHold();
		}
		else if (!beingHeld) {
			
			//reset recursion flag
			ResetRecursionFlag();
			
			//if one of the dragged tiles snaps to something, store the one with the shortest snap distance
			dragSnapped = CheckSnapRecursively(this, data, mouseLocation - offset);
			//Debug.Log(dragSnapped);
			
			//Vector3 movement = Camera.main.transform.localPosition + ray.direction * Vector3.Distance(transform.localPosition, Camera.main.transform.localPosition) - offset - transform.localPosition;
			Vector3 movement = mouseLocation - offset - transform.localPosition;
			movement = new Vector3(movement.x, movement.y, 0f);
			
			//Vector3 distanceBetweenMouseAndTransform = transform.localPosition - (Camera.main.transform.localPosition + ray.direction * Vector3.Distance(transform.localPosition, Camera.main.transform.localPosition) - offset);
			Vector3 distanceBetweenMouseAndTransform = transform.localPosition - (mouseLocation - offset);
			
			//reset recursion flag
			ResetRecursionFlag();
			
			if (!radialMenu.activeSelf && getRadialSelected() == -1) {
				if (dragSnapped == null || distanceBetweenMouseAndTransform.magnitude > snapStickiness) {
					MoveRecursively(this, movement);
					snapped = null;
					dragSnapped = null;
				}
				else {
					//snap distance is location offset by the dimensions of the dragged tile
					MoveRecursively(this, dragSnapped.snapMovement);
				}
			}
		}
	}
	
	//when drag ends, reset the tile and, if something has been snapped to,
	//link adjacents and then combine the groups
    public void OnEndDragDelegate(PointerEventData data) {
		//Debug.Log(HasAdjacents());
		
		//Create a ray going from the camera through the mouse position
		//Ray ray = Camera.main.ScreenPointToRay(data.localPosition);
		RaycastHit hit;
		
			//Debug.DrawRay(ray.origin, ray.direction * 100f, Color.green, 5, true);
		//checks if pointer is over trash
		if (Physics.Raycast(ray, out hit, 100f)) {
			//Debug.Log(hit.collider.tag);
			if(hit.collider.tag.Equals("trash")) {
				ResetRecursionFlag();
				TrashTilesRecursively(this);
			}
		}
		
        transform.localPosition = new Vector3(transform.localPosition.x, transform.localPosition.y, 0f);
		ResetRecursionFlag();
		ChangeLayersRecursively(this, 8);
		
		//dragSnapped is whatever tile was the closest to snapping to something while we were dragging
		//dragSnapped.snapped is the tile dragSnapped is snapping to
		if (dragSnapped != null) {
			ResetRecursionFlag();
			
			HashSet<DraggyTile> buildSet = new HashSet<DraggyTile>();
			
			bool temp = BuildSetRecursively(dragSnapped, dragSnapped.snapped, dragSnapped.snapDir, buildSet, new Vector2(0,0));
			//Debug.Log(temp);
			if (temp) {
				dragSnapped.adjacent[dragSnapped.snapDir] = dragSnapped.snapped;
				dragSnapped.snapped.adjacent[(dragSnapped.snapDir + 2) % 4] = dragSnapped;
				LinkTheFlames(buildSet);
			}
			else {
				shakeScreen(4);
			}
		}
		dragSnapped = null;
	}

	//check if any tile is close enough to snap to anything, return closest snapped tile if any
	//mouseLocationPlusOffset makes sure we don't end up snapping for one tile and then snapping somewhere else for an adjacent tile
	//by keeping track of where the current tile's position was prior to any snapping
	public static DraggyTile CheckSnapRecursively(DraggyTile tile, PointerEventData data, Vector3 mouseLocationPlusOffset) {
		
		tile.alreadyFlagged = true;
		
		/*//Create a ray going from the camera through the mouse position
		Ray ray = Camera.main.ScreenPointToRay(data.localPosition);*/
		
		//keeps track of which snapped tile is closest
		DraggyTile closestSnap = null;
		
		for (int dir = 0; dir < 4; dir++) {
			if (tile.adjacent[dir] != null && !tile.adjacent[dir].alreadyFlagged) {
				Vector3 newOffset = new Vector3();
				
				if (dir == 0)
					newOffset = new Vector3(0, ExpressionToDimension(tile.width.AddExpression(tile.adjacent[dir].width)) / 2 * tile.transform.parent.localScale.y, 0);
				if (dir == 1)
					newOffset = new Vector3(ExpressionToDimension(tile.length.AddExpression(tile.adjacent[dir].length)) / 2 * tile.transform.parent.localScale.x, 0, 0);
				if (dir == 2)
					newOffset = new Vector3(0, -ExpressionToDimension(tile.width.AddExpression(tile.adjacent[dir].width)) / 2 * tile.transform.parent.localScale.y, 0);
				if (dir == 3)
					newOffset = new Vector3(-ExpressionToDimension(tile.length.AddExpression(tile.adjacent[dir].length)) / 2 * tile.transform.parent.localScale.x, 0, 0);
				
				DraggyTile temp = CheckSnapRecursively(tile.adjacent[dir], data, mouseLocationPlusOffset + newOffset);
				if (closestSnap == null || (temp != null && Vector3.Distance(temp.transform.localPosition, temp.snapped.transform.localPosition) < Vector3.Distance(closestSnap.transform.localPosition, closestSnap.snapped.transform.localPosition)))
					closestSnap = temp;
			}
		}
		
		SphereCollider[] snapColliders = tile.GetComponentsInChildren<SphereCollider>();
		for (int i = 0; i < 4; i++) {
			//layer 8 is the snap layer
			int layerMask = 1 << 8;
			
			RaycastHit hit;
			//checks if anything is within a radius of the snap collider
			Vector3 castLocation = new Vector3();
			if (tile.snapped == null)
				castLocation = tile.transform.localPosition;
			else
				castLocation = mouseLocationPlusOffset;
			
			Vector3 scaledColliderCenter = new Vector3 (snapColliders[i].center.x,// * snapColliders[i].transform.parent.parent.localScale.x,
														snapColliders[i].center.y,// * snapColliders[i].transform.parent.parent.localScale.y,
														snapColliders[i].center.z);// * snapColliders[i].transform.parent.parent.localScale.z);
			
//Debug.Log(snapColliders[i].transform.parent.parent.localScale);
//Debug.DrawRay(tile.transform.parent.TransformPoint(tile.transform.localPosition), new Vector3(0, 0, 1000f), Color.green);
//Debug.DrawRay(tile.transform.position, new Vector3(0, 0, 1000f), Color.red);
Debug.DrawRay(tile.transform.parent.TransformPoint(castLocation + scaledColliderCenter - new Vector3(0, 0, 5)), new Vector3(0, 0, 1000f), Color.green);
			if (Physics.SphereCast(tile.transform.parent.TransformPoint(castLocation + scaledColliderCenter - new Vector3(0, 0, 5)), tile.snapStrength, new Vector3(0, 0, 1), out hit, 7f, layerMask)) {
				//Debug.Log("hit");
				//check if the right colliders are touching
				for (int dir = 0; dir < 4; dir++) {
					if (tile.adjacent[dir] == null &&
						snapColliders[i].tag.Equals(tagPairs[dir, 0]) && hit.collider.tag.Equals(tagPairs[dir, 1]) && //check if colliders are on the right sides to snap
						((dir % 2 == 0 && tile.length.IsEqual(hit.collider.transform.parent.GetComponent<Tile>().GetLength())) ||
						(dir % 2 == 1 && tile.width.IsEqual(hit.collider.transform.parent.GetComponent<Tile>().GetWidth())))) { //check to see if the dimensions along the snapped side match
						//Debug.Log(snapColliders[i].tag + ", " + hit.collider.tag);
						//snap to the center of the collider we hit
						Vector3 scaledColliderCenter2 = new Vector3 (
														hit.collider.GetComponent<SphereCollider>().center.x, // * tile.transform.parent.localScale.x,
														hit.collider.GetComponent<SphereCollider>().center.y, // * tile.transform.parent.localScale.y,
														hit.collider.GetComponent<SphereCollider>().center.z); // * tile.transform.parent.localScale.z);
														
						Vector3 snapLocation = hit.collider.transform.parent.localPosition + scaledColliderCenter2;
Debug.DrawRay(tile.transform.parent.TransformPoint(hit.collider.transform.parent.localPosition), new Vector3(0, 0, 1000f), Color.red);
						//snap distance is location offset by the dimensions of the dragged tile
						Vector3 snapDistance = snapLocation + new Vector3(
							(Tile.ExpressionToDimension(tile.length) * tile.transform.parent.localScale.x / 2) * (dir % 2) * -(2 - dir), //offset in x direction if dir is east or west
							(Tile.ExpressionToDimension(tile.width) * tile.transform.parent.localScale.y / 2) * ((dir + 1) % 2) * (dir - 1), //offset in y direction if dir is north or south
							0f) - tile.transform.localPosition;
						
							if (closestSnap == null || snapDistance.magnitude < Vector3.Distance(closestSnap.transform.localPosition, closestSnap.snapped.transform.localPosition)) {
								//clear out previous snap since this one is closer
								if (closestSnap != null) {
									closestSnap.snapped = null;
									closestSnap.snapDir = -1;
								}
								tile.snapped = hit.collider.transform.parent.GetComponent<DraggyTile>();
								tile.snapDir = dir;
								tile.snapMovement = snapDistance;
								closestSnap = tile;
							}
					}
				}
				
			}
		}
		return closestSnap;
	}
	
	//if this tile is adjacent to one that's moving, move it and any adjacent tiles
	//offset is where to move it to, tile is the tile being moved
	public static void MoveRecursively(DraggyTile tile, Vector3 offset) {
		tile.transform.localPosition = tile.transform.localPosition + offset;
		tile.alreadyFlagged = true;
		//if there is an adjacent tile and we haven't set alreadyMoved yet on this loop, recur
		for (int dir = 0; dir < 4; dir++) {
			if (tile.adjacent[dir] != null && !tile.adjacent[dir].alreadyFlagged)	
				MoveRecursively(tile.adjacent[dir], offset);
		}
		//tile.alreadyMoved = false;
	}
	
	//when a tile is broken out of a group, clear the adjacent links
	//if any adjacent tiles are offscreen, move them onscreen
	public void ClearAdjacents() {
		for (int dir = 0; dir < 4; dir++) {
			if (adjacent[dir] != null) {
				ResetRecursionFlag();
				this.alreadyFlagged = true;
				
				if (dir == 0) {
					if (adjacent[0].transform.localPosition.y > screenTop)
						DraggyTile.MoveRecursively(adjacent[0], new Vector3 (0f, screenTop - adjacent[0].transform.localPosition.y, 0f));
//Debug.Log("0");
				}
				else if (dir == 1) {
					adjacent[1].EnableSideLabels(2, true);
					if (adjacent[1].transform.localPosition.x > screenRight) {
						DraggyTile.MoveRecursively(adjacent[1], new Vector3 (screenRight - adjacent[1].transform.localPosition.x, 0f, 0f));
					}
//Debug.Log("1");
				}
				else if (dir == 2) {
					adjacent[2].EnableSideLabels(1, true);
					if (adjacent[2].transform.localPosition.y < screenBot)
						DraggyTile.MoveRecursively(adjacent[2], new Vector3 (0f, screenBot - adjacent[2].transform.localPosition.y, 0f));
//Debug.Log("2");
				}
				else if (dir == 3) {
					if (adjacent[3].transform.localPosition.x < screenLeft)
						DraggyTile.MoveRecursively(adjacent[3], new Vector3 (screenLeft - adjacent[3].transform.localPosition.x, 0f, 0f));
//Debug.Log("3");
				}
				
				adjacent[dir].adjacent[(dir + 2) % 4] = null;
				adjacent[dir].CombineSideLabels(adjacent[dir]);
				ChangeLayersRecursively(adjacent[dir], 8);
				adjacent[dir] = null;
				ResetSideLabels();
			}
		}
		
		isShakeable = false;
		shakeScreen(2);
		isShakeable = true;
	}
	
	//does a tile have any adjacent tiles
	public bool HasAdjacents() {
		return (adjacent[0] != null ||
				adjacent[1] != null ||
				adjacent[2] != null ||
				adjacent[3] != null);
	}
	
	//checks to see if there are any tiles in the same row or column that have mismatched dimensions
	public static bool CheckAdjacentDimensionsRecursively(DraggyTile tile, int dir,
		LinkedList<Expression> columns, LinkedListNode<Expression> currentColumn,
		LinkedList<Expression> rows, LinkedListNode<Expression> currentRow) {
			
		tile.alreadyFlagged = true;
		
		bool temp = true;
		
		if (dir == 0) {
			if (currentRow.Next == null)
				rows.AddLast(tile.width);
			else if (!currentRow.Next.Value.IsEqual(tile.width))
				return false;
		
			for (int i = 0; i < 4; i++)
				if (tile.adjacent[i] != null && !tile.adjacent[i].alreadyFlagged)
					temp = temp && CheckAdjacentDimensionsRecursively(tile.adjacent[i], i, columns, currentColumn, rows, currentRow.Next);
		}
		
		if (dir == 1) {
			if (currentColumn.Next == null)
				columns.AddLast(tile.length);
			else if (!currentColumn.Next.Value.IsEqual(tile.length))
				return false;
		
			for (int i = 0; i < 4; i++)
				if (tile.adjacent[i] != null && !tile.adjacent[i].alreadyFlagged)
					temp = temp && CheckAdjacentDimensionsRecursively(tile.adjacent[i], i, columns, currentColumn.Next, rows, currentRow);
		}
		
		if (dir == 2) {
			if (currentRow.Previous == null)
				rows.AddFirst(tile.width);
			else if (!currentRow.Previous.Value.IsEqual(tile.width))
				return false;
		
			for (int i = 0; i < 4; i++)
				if (tile.adjacent[i] != null && !tile.adjacent[i].alreadyFlagged)
					temp = temp && CheckAdjacentDimensionsRecursively(tile.adjacent[i], i, columns, currentColumn, rows, currentRow.Previous);
		}
		
		if (dir == 3) {
			if (currentColumn.Previous == null)
				columns.AddFirst(tile.length);
			else if (!currentColumn.Previous.Value.IsEqual(tile.length))
				return false;
		
			for (int i = 0; i < 4; i++)
				if (tile.adjacent[i] != null && !tile.adjacent[i].alreadyFlagged)
					temp = temp && CheckAdjacentDimensionsRecursively(tile.adjacent[i], i, columns, currentColumn.Previous, rows, currentRow);
		}
		
		return temp;
	}
	
	//add all tiles in group to set, return false if there is a failure
	//tile is the tile we're currently at, prev is the tile we came from, dir is the side of the current tile that prev is on
	public static bool BuildSetRecursively(DraggyTile tile, DraggyTile prev, int dir, HashSet<DraggyTile> tileSetR, Vector2 coords) {
			
		if (tile == null)
			return true;
		
		tile.alreadyFlagged = true;
		
		bool temp = true;
		
		//Debug.Log("checking for " + tile.width + ", " + tile.length + " at " + coords);
		tile.coords = coords;
		//checks to see if tile set contains a tile that is a problem
		if (DraggyTile.FindProblemTile(tileSetR, tile)) {
			//Debug.Log("failed");
			return false;
		}
		
		tileSetR.Add(tile);
		//tile.adjacent[dir] = prev;
		//prev.adjacent[(dir + 2) % 4] = tile;
		
		for (int i = 0; i < 4; i++) {
		//Debug.Log(coords + new Vector2((i - 2) % 2, (i - 1) % 2));
			if (i == dir && !prev.alreadyFlagged)
				temp = temp && BuildSetRecursively(prev, tile, (i + 2) % 4, tileSetR, coords + new Vector2((i - 2) % 2, (i - 1) % 2));
			else if (tile.adjacent[i] != null && !tile.adjacent[i].alreadyFlagged)
				temp = temp && BuildSetRecursively(tile.adjacent[i], tile, (i + 2) % 4, tileSetR, coords + new Vector2((i - 2) % 2, (i - 1) % 2));
		}
		
		//Debug.Log(temp);
		return temp;
		
	}
	
	//connect any newly adjacent tiles
	public static void LinkTheFlames(HashSet<DraggyTile> hSet) {
		foreach (DraggyTile t1 in hSet) {
			if (t1.adjacent[0] == null || t1.adjacent[1] == null || t1.adjacent[2] == null || t1.adjacent[3] == null) {
			
				foreach (DraggyTile t2 in hSet) {
					//if tiles are in the same column
					if (t1.coords.x == t2.coords.x) {
						//
						if (t1.coords.y == t2.coords.y - 1) {
							t1.adjacent[2] = t2;
							t2.adjacent[0] = t1;
							t2.EnableSideLabels(1, false);
						}
						if (t1.coords.y == t2.coords.y + 1) {
							t1.adjacent[0] = t2;
							t2.adjacent[2] = t1;
							t1.EnableSideLabels(1, false);
						}
					}
					//if tiles are in the same row
					if (t1.coords.y == t2.coords.y) {
						if (t1.coords.x == t2.coords.x - 1) {
							t1.adjacent[3] = t2;
							t2.adjacent[1] = t1;
							t1.EnableSideLabels(2, false);
						}
						if (t1.coords.x == t2.coords.x + 1) {
							t1.adjacent[1] = t2;
							t2.adjacent[3] = t1;
							t2.EnableSideLabels(2, false);
						}
					}
				}	
			}
			t1.CombineSideLabels(t1);
		}
	}
	
	//adjust the labels of any tiles adjacent to t1 so that adjacent sides are combined
	public void CombineSideLabels(DraggyTile t1) {
		Expression sideLength = new Expression();
		DraggyTile temp = t1;
		//find the topmost block in this segment
		while (temp.adjacent[3] == null && temp.adjacent[0] != null && temp.adjacent[0].adjacent[3] == null)
			temp = temp.adjacent[0];
		while (temp != null && temp.adjacent[3] == null) {
			//if there is a previous block, turn its label off
			if (temp.adjacent[0] != null)
				temp.adjacent[0].EnableSideLabels(2, false);
			sideLength = sideLength.AddExpression(temp.width);
			//update the side length for the current block with our current sum
			temp.SetSideLabel(2, sideLength.ToString(), new Vector3(0, ExpressionToDimension(sideLength.SubtractExpression(temp.width)) / 2 * scale, 0));
			temp.EnableSideLabels(2, true);
			temp = temp.adjacent[2];
		}
		
		sideLength = new Expression();
		temp = t1;
		//find the rightmost block in this segment
		while (temp.adjacent[0] == null && temp.adjacent[1] != null && temp.adjacent[1].adjacent[0] == null)
			temp = temp.adjacent[1];
		while (temp != null && temp.adjacent[0] == null) {
			//if there is a previous block, turn its label off
			if (temp.adjacent[1] != null)
				temp.adjacent[1].EnableSideLabels(1, false);
			sideLength = sideLength.AddExpression(temp.length);
			//update the side length for the current block with our current sum
			temp.SetSideLabel(1, sideLength.ToString(), new Vector3(ExpressionToDimension(sideLength.SubtractExpression(temp.length)) / 2 * scale, 0, 0));
			temp.EnableSideLabels(1, true);
			temp = temp.adjacent[3];
		}
	}
	
	//destroy tile and any adjacent tiles
	static void TrashTilesRecursively(DraggyTile tile) {
		tile.alreadyFlagged = true;
		//if there is an adjacent tile and we haven't set alreadyMoved yet on this loop, recur
		for (int dir = 0; dir < 4; dir++) {
			if (tile.adjacent[dir] != null && !tile.adjacent[dir].alreadyFlagged)	
				TrashTilesRecursively(tile.adjacent[dir]);
		}
		
		Destroy(tile.gameObject);
	}
	
	//reset whether or not tiles have been checked when doing recursion stuff
	public void ResetRecursionFlag() {
		allTiles = FindObjectsOfType<DraggyTile>();
		
		for (int i = 0; i < allTiles.Length; i++) {
			allTiles[i].alreadyFlagged = false;
		}
		
	}

    public void OnPointerClickDelegate(PointerEventData data) {
		if (rotateTimer < rotateTime)
			RotateTile();
		rotateTimer = 1f;
	}
	
	
	//switches length and width of a tile
	//if it has adjacent tiles, rotate the tiles around it as well
	public void RotateTile() {
		if (!IsBusy()) {
			ResetRecursionFlag();
			RotateRecursively(this, -1, transform.localPosition);
		}
		
		//DraggyTile tempAdj = adjacent[3];
		//for (int i = 3; i >= 0; i--) {
		//	if (i > 0)
		//		adjacent[i] = adjacent[(i - 1) % 4];
		//	else
		//		adjacent[i] = tempAdj;
		//}
		
		//CombineSideLabels(this);
	}
	
	public static void RotateRecursively(DraggyTile tile, int fromDir, Vector3 origin) {
		tile.alreadyFlagged = true;
		
		Expression temp = new Expression();
		temp.SetExpression(tile.width);
		tile.width.SetExpression(tile.length);
		tile.length.SetExpression(temp);
		MeshManager meshman = tile.GetComponent<MeshManager>();
		//meshman.SetDimensions(ExpressionToDimension(length), ExpressionToDimension(width), scale);
		tile.SetSnapColliders(ExpressionToDimension(tile.length), ExpressionToDimension(tile.width));
		
		if (fromDir == -1) {
			meshman.RotateShape();
		}
		else {
			//find the new location relative to the tile we're rotating around
			Vector3 offset = Quaternion.Euler(0, 0, -90) * (tile.transform.localPosition - origin);
			meshman.RotateShape(origin, offset);
		}
		
		for (int dir = 0; dir < 4; dir++) {
			if (tile.adjacent[dir] != null && !tile.adjacent[dir].alreadyFlagged)	
				RotateRecursively(tile.adjacent[dir], dir, origin);
		}
		
		DraggyTile tempAdj = tile.adjacent[3];
		for (int i = 3; i >= 0; i--) {
			if (i > 0) {
				tile.adjacent[i] = tile.adjacent[(i - 1) % 4];
			}
			else {
				tile.adjacent[i] = tempAdj;
			}
			if (i == 0 || i == 3) {
				if (tile.adjacent[i] != null)
					tile.EnableSideLabels(i % 2 + 1, false);
				else
					tile.EnableSideLabels(i % 2 + 1, true);
			}
		}
		
		tile.CombineSideLabels(tile);
	}

	//while the pointer is held down on a tile, a timer counts up
	//used to disconnect tiles or open radial menu
    public void OnPointerDownDelegate(PointerEventData data) {
		if (!beingHeld) {
		beingHeld = true;
		timer = 0f;
//debugBlurb.text = "OnPointerDown";
		rotateTimer = 0f;
		}
		//MeshManager meshman = GetComponent<MeshManager>();
		//meshman.SetDimensions(ExpressionToDimension(length), ExpressionToDimension(width));
			//debugBlurb.text = debugBlurb.text + "pointerDown";
	}

	//clear out hold timer and close radial menu
    public void OnPointerUpDelegate(PointerEventData data) {
		beingHeld = false;
		timer = 0f;
//debugBlurb.text = "OnPointerUp";
		if (radialInitialized())
			closeRadialMenu(false, true);
		//debugBlurb.text = debugBlurb.text + "pointerUp";
	}
	
	public bool IsShakeable() {
		return isShakeable;
	}

	public DraggyTile GetAdjacent(int dir) {
		if (adjacent != null)
			return adjacent[dir];
		else
			return null;
	}

    public void OnPointerExitDelegate(PointerEventData data) {
	}
	
	//if two tiles are in the same coordinates, return true
	//if two tiles are in the same column but have different lengths, return true
	//if two tiles are in the same row but have different widths, return true
	static bool FindProblemTile(HashSet<DraggyTile> hset, DraggyTile t1) {
		bool temp = false;
		
		foreach (DraggyTile t2 in hset) {
			if (t1.coords == t2.coords) {
//Debug.Log(t1.width + ", " + t1.length + "at" + t1.coords + "failed coords match with " + t2.width + ", " + t2.length + " at " + t2.coords);
				temp = true;
			}
			else if (t1.coords.x == t2.coords.x) {
				if (t1.length.IsEqual(t2.length))
					temp = temp || false;
				else {
//Debug.Log(t1.width + ", " + t1.length + "at" + t1.coords + "failed column match with " + t2.width + ", " + t2.length + " at " + t2.coords);
					temp = true;
				}
			}
			else if (t1.coords.y == t2.coords.y) {
				if (t1.width.IsEqual(t2.width))
					temp = temp || false;
				else {
//Debug.Log(t1.width + ", " + t1.length + "at" + t1.coords + "failed row match with " + t2.width + ", " + t2.length + " at " + t2.coords);
					temp = true;
				}
			}
		}
		
		return temp;
	}
	
	void SetDelegates() {
        EventTrigger trigger = GetComponent<EventTrigger>();
		
        //begin drag event
        EventTrigger.Entry entry = new EventTrigger.Entry();
        entry.eventID = EventTriggerType.BeginDrag;
        entry.callback.AddListener((data) => { OnBeginDragDelegate((PointerEventData)data); });
        trigger.triggers.Add(entry);
		
		//drag event
		entry = new EventTrigger.Entry();
        entry.eventID = EventTriggerType.Drag;
        entry.callback.AddListener((data) => { OnDragDelegate((PointerEventData)data); });
        trigger.triggers.Add(entry);
		
		//end drag event
		entry = new EventTrigger.Entry();
        entry.eventID = EventTriggerType.EndDrag;
        entry.callback.AddListener((data) => { OnEndDragDelegate((PointerEventData)data); });
        trigger.triggers.Add(entry);
		
		//pointer down event
		entry = new EventTrigger.Entry();
        entry.eventID = EventTriggerType.PointerDown;
        entry.callback.AddListener((data) => { OnPointerDownDelegate((PointerEventData)data); });
        trigger.triggers.Add(entry);
		
		//pointer up event
		entry = new EventTrigger.Entry();
        entry.eventID = EventTriggerType.PointerUp;
        entry.callback.AddListener((data) => { OnPointerUpDelegate((PointerEventData)data); });
        trigger.triggers.Add(entry);
		
		//pointer exit event
		entry = new EventTrigger.Entry();
        entry.eventID = EventTriggerType.PointerExit;
        entry.callback.AddListener((data) => { OnPointerExitDelegate((PointerEventData)data); });
        trigger.triggers.Add(entry);
		
		//pointer click event
		entry = new EventTrigger.Entry();
        entry.eventID = EventTriggerType.PointerClick;
        entry.callback.AddListener((data) => { OnPointerClickDelegate((PointerEventData)data); });
        trigger.triggers.Add(entry);
	}
}