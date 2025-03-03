extends Camera3D

static var lastHighlighted

# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	pass # Replace with function body.


# Called every frame. 'delta' is the elapsed time since the previous frame.
func _process(delta: float) -> void:
	pass
const ray_length = 1000

func _input(event):
	if event is InputEventMouseMotion: #InputEventMouseButton and event.pressed and event.button_index == 1:
		var hit = RaycastSystem.get_raycast_hit_object()
		if (hit != lastHighlighted && hit != null):
			var h = hit.get_child(0).get_surface_override_material(0)
			h.albedo_color = Color(1, 1, 1)
		if (hit != lastHighlighted && lastHighlighted != null):
			var h = lastHighlighted.get_child(0).get_surface_override_material(0)
			h.albedo_color = Color(0.8, 0.8, 0.8)
		lastHighlighted = hit
