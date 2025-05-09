using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class PuzzlePiece : MonoBehaviour
{
    private const float SnapThreshold = 0.7f; // How close to snap
    private const float ReturnAnimSpeed = 10f;
    public int pieceID;
    public Vector3 targetPosition; // The correct position on the board
    public Vector3 scramblePosition; // The initial position in the scramble area
    public SpriteRenderer imageDisplayRenderer;

    private bool _isDragging;
    private bool _isPlaced;
    private Vector3 _offset;
    private PuzzleManager _puzzleManager;
    private SpriteRenderer _spriteRenderer;

    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void OnMouseDown()
    {
        if (_isPlaced || !_puzzleManager.IsGameActive()) return;

        _isDragging = true;
        _offset = transform.position - GetMouseWorldPosition();
        _spriteRenderer.sortingOrder = 10; // Bring to front while dragging
        _puzzleManager.PickupPiece(); // Optional: sound or other effects
    }

    private void OnMouseDrag()
    {
        if (_isDragging && _puzzleManager.IsGameActive()) transform.position = GetMouseWorldPosition() + _offset;
    }

    private void OnMouseUp()
    {
        if (_isDragging && _puzzleManager.IsGameActive())
        {
            _isDragging = false;
            _spriteRenderer.sortingOrder = 1; // Reset sorting order

            var distanceToTarget = Vector3.Distance(transform.position, targetPosition);

            if (distanceToTarget < SnapThreshold)
            {
                // Check if this slot is already taken by the correct piece or if this piece is already placed
                if (!_puzzleManager.IsSlotTaken(pieceID, targetPosition) && !_isPlaced)
                {
                    transform.position = targetPosition;
                    _isPlaced = true;
                    _puzzleManager.PiecePlacedCorrectly(this);
                }
                else if (_isPlaced) // Already correctly placed, snap back
                {
                    transform.position = targetPosition;
                }
                else // Slot taken or some other issue, return to scramble
                {
                    _puzzleManager.PiecePlacedIncorrectly();
                    StartCoroutine(ReturnToScramblePosition());
                }
            }
            else
            {
                _puzzleManager.PiecePlacedIncorrectly();
                StartCoroutine(ReturnToScramblePosition());
            }
        }
    }

    public void Initialize(int id, Vector3 targetPos, Vector3 scramblePos, Sprite pieceSprite, PuzzleManager manager)
    {
        pieceID = id;
        targetPosition = targetPos;
        scramblePosition = scramblePos;
        transform.position = scramblePos;
        _puzzleManager = manager;

        _spriteRenderer.sprite = pieceSprite; // This sprite should be the actual image slice

        // If PuzzlePieceShape.png is just a *mask* or *frame*, you'd handle that differently
        // e.g., have a child object with the image slice, and this parent has the frame.
        // For simplicity, we're setting the main sprite to the image slice.
        // To make it "look" like a puzzle piece, the `pieceSprite` itself would need that shape.
        // Or, we use a material with a mask, or a UI Mask component if pieces are UI Images.
        // Let's assume `pieceSprite` is the cut-out image.
        // If you have a generic `PuzzlePieceShape.png` sprite, you might want to use it
        // as the primary sprite and *tint* it or use a shader to apply the image fragment.
        // A simpler approach for "realistic shape" without complex shaders:
        // Your `pieceSprite` (the slice from the main image) is square.
        // The `PuzzlePiece_Prefab` has a `SpriteRenderer` whose `Sprite` is `PuzzlePieceShape.png`.
        // Then, this `pieceSprite` (image slice) is applied to a *child* GameObject's SpriteRenderer,
        // scaled and positioned to fit within the `PuzzlePieceShape.png`.
        // For this example, we'll assume `pieceSprite` *is* the shaped piece.

        imageDisplayRenderer.sprite = pieceSprite;
    }

    private static Vector3 GetMouseWorldPosition()
    {
        var mousePoint = Input.mousePosition;
        mousePoint.z = Camera.main.nearClipPlane + 10; // Distance from camera
        return Camera.main.ScreenToWorldPoint(mousePoint);
    }

    private IEnumerator ReturnToScramblePosition()
    {
        var startPosition = transform.position;
        var journey = 0f;
        var duration = Vector3.Distance(startPosition, scramblePosition) / ReturnAnimSpeed;
        if (duration == 0) duration = 0.1f; // Avoid division by zero if already there

        while (journey < duration)
        {
            journey += Time.deltaTime;
            var percent = Mathf.Clamp01(journey / duration);
            transform.position = Vector3.Lerp(startPosition, scramblePosition, percent);
            yield return null;
        }

        transform.position = scramblePosition;
    }

    public bool IsPlaced()
    {
        return _isPlaced;
    }

    public void ResetPiece(Vector3 newScramblePos)
    {
        _isPlaced = false;
        _isDragging = false;
        scramblePosition = newScramblePos;
        transform.position = scramblePosition;
        _spriteRenderer.sortingOrder = 1;
    }
}