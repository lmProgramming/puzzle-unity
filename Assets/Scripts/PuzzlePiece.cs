using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class PuzzlePiece : MonoBehaviour
{
    private const float SnapThreshold = 0.7f;
    private const float ReturnAnimSpeed = 10f;
    public int pieceID;
    public Vector3 targetPosition;
    public Vector3 scramblePosition;
    public SpriteRenderer imageDisplayRenderer;
    private Camera _camera;

    private bool _isDragging;
    private bool _isPlaced;
    private Vector3 _offset;
    private PuzzleManager _puzzleManager;
    private SpriteRenderer _spriteRenderer;

    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _camera = Camera.main;
    }

    private void OnMouseDown()
    {
        if (_isPlaced || !_puzzleManager.IsGameActive()) return;

        _isDragging = true;
        _offset = transform.position - GetMouseWorldPosition();
        _spriteRenderer.sortingOrder = 10;
        _puzzleManager.PickupPiece();
    }

    private void OnMouseDrag()
    {
        if (_isDragging && _puzzleManager.IsGameActive()) transform.position = GetMouseWorldPosition() + _offset;
    }

    private void OnMouseUp()
    {
        if (!_isDragging || !_puzzleManager.IsGameActive()) return;

        _isDragging = false;
        _spriteRenderer.sortingOrder = 1;

        var distanceToTarget = Vector3.Distance(transform.position, targetPosition);

        if (distanceToTarget < SnapThreshold)
        {
            if (!_puzzleManager.IsSlotTaken(pieceID, targetPosition) && !_isPlaced)
            {
                transform.position = targetPosition;
                _isPlaced = true;
                _puzzleManager.PiecePlacedCorrectly(this);
            }
            else if (_isPlaced)
            {
                transform.position = targetPosition;
            }
            else
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

    public void Initialize(int id, Vector3 targetPos, Vector3 scramblePos, Sprite pieceSprite, PuzzleManager manager)
    {
        pieceID = id;
        targetPosition = targetPos;
        scramblePosition = scramblePos;
        transform.position = scramblePos;
        _puzzleManager = manager;

        _spriteRenderer.sprite = pieceSprite;

        imageDisplayRenderer.sprite = pieceSprite;
    }

    private Vector3 GetMouseWorldPosition()
    {
        var mousePoint = Input.mousePosition;
        mousePoint.z = _camera.nearClipPlane + 10;
        return _camera.ScreenToWorldPoint(mousePoint);
    }

    private IEnumerator ReturnToScramblePosition()
    {
        var startPosition = transform.position;
        var journey = 0f;
        var duration = Vector3.Distance(startPosition, scramblePosition) / ReturnAnimSpeed;
        if (duration == 0) duration = 0.1f;

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