extends Node2D

var dragging = false
@onready var click_area = get_child(0).shape.extents * 2 # Size of the sprite.

func _input(event):
	drag(event)
	
func drag(event):
	# Start dragging if the click is on the sprite.
	if not dragging and event.is_action_pressed("DragTile"):
		if Rect2($".".position - click_area / 2, click_area).has_point(event.position):
			dragging = true
			$".".scale.x = 1.1
			$".".scale.y = 1.1
	# Stop dragging if the button is released.
	if dragging and event.is_action_released("DragTile"):
		dragging = false
		$".".scale.x = 1
		$".".scale.y = 1
	# While dragging, move the sprite with the mouse.
	if dragging and (event is InputEventMouseMotion or event is InputEventScreenDrag):
		$".".position = event.position
