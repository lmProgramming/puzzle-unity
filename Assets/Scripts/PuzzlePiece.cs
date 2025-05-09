using Cysharp.Threading.Tasks;
using LM;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
public class PuzzlePiece : MonoBehaviour
{
    private const float SnapThreshold = 0.7f;
    private const float ReturnAnimSpeed = 10f;

    public int pieceID;
    public Vector3 targetPosition;

    public Vector3 scramblePosition;

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
        if (_isPlaced || !_puzzleManager || !_puzzleManager.IsGameActive()) return;

        _isDragging = true;
        _offset = transform.position - (Vector3)GameInput.WorldPointerPosition;
        _spriteRenderer.sortingOrder = 10;
    }

    private void OnMouseDrag()
    {
        if (_isDragging && _puzzleManager && _puzzleManager.IsGameActive())
            transform.position = (Vector3)GameInput.WorldPointerPosition + _offset;
    }

    private void OnMouseUp()
    {
        if (!_isDragging || !_puzzleManager || !_puzzleManager.IsGameActive()) return;

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
                ReturnToScramblePosition().Forget();
            }
        }
        else
        {
            _puzzleManager.PiecePlacedIncorrectly();
            ReturnToScramblePosition().Forget();
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
    }

    private async UniTask ReturnToScramblePosition()
    {
        var startPosition = transform.position;
        var journey = 0f;

        var duration = Vector3.Distance(startPosition, scramblePosition) / ReturnAnimSpeed;
        if (duration <= 0.001f)
        {
            transform.position = scramblePosition;
            return;
        }

        while (journey < duration)
        {
            journey += Time.deltaTime;
            var percent = Mathf.Clamp01(journey / duration);
            transform.position = Vector3.Lerp(startPosition, scramblePosition, percent);
            await UniTask.Yield();
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
        if (_spriteRenderer) _spriteRenderer.sortingOrder = 1;
    }
}