using System.Collections.Generic;
using System.Linq;
using LM;
using TMPro;
using UnityEngine;

public class PuzzleManager : MonoBehaviour
{
    private const int TextureResolutionPerPiece = 128;

    [Header("Puzzle Settings")]
    public int rows = 4;

    public int cols = 4;
    public float pieceSize = 1.5f;
    public GameObject puzzlePiecePrefab;
    public GameObject puzzlePieceOutlinePrefab;
    public Sprite puzzlePieceBaseShape;

    [Header("Image Generation")]
    public Color color1 = Color.red;

    public Color color2 = Color.blue;
    public Color color3 = Color.green;
    public Color color4 = Color.yellow;

    [Header("Layout")]
    public Transform boardOrigin;

    public Transform scrambleOrigin;

    [Header("UI")]
    public TextMeshProUGUI timerText;

    public TextMeshProUGUI winText;

    [Header("Audio")]
    public AudioClip correctPlacementSound;

    public AudioClip incorrectPlacementSound;
    private readonly HashSet<int> _correctlyPlacedPieceIDs = new();

    private readonly List<PuzzlePiece> _pieces = new();
    private readonly Dictionary<int, Vector3> _targetSlots = new();
    private AudioSource _audioSource;
    private bool _gameActive;
    private bool _gameWon;
    private Texture2D _sourceTexture;


    private float _startTime;

    private void Start()
    {
        _audioSource = GetComponent<AudioSource>();

        winText.gameObject.SetActive(false);
        GenerateAndSetupPuzzle();
    }

    private void Update()
    {
        if (!_gameActive || _gameWon) return;

        var t = Time.time - _startTime;
        var minutes = ((int)t / 60).ToString("00");
        var seconds = (t % 60).ToString("00.00");
        timerText.text = "Time: " + minutes + ":" + seconds;
    }

    private void GenerateAndSetupPuzzle()
    {
        ClearPuzzle();

        GenerateSourceTexture();
        CreatePuzzlePieces();
        ShuffleAndPlacePieces();

        _startTime = Time.time;
        _gameActive = true;
        _gameWon = false;
        winText.gameObject.SetActive(false);
        timerText.text = "Time: 00:00.00";
        _correctlyPlacedPieceIDs.Clear();
    }

    private void ClearPuzzle()
    {
        foreach (var piece in _pieces)
            Destroy(piece.gameObject);
        _pieces.Clear();
        _targetSlots.Clear();
    }

    private void GenerateSourceTexture()
    {
        var textureWidth = cols * TextureResolutionPerPiece;
        var textureHeight = rows * TextureResolutionPerPiece;
        _sourceTexture = new Texture2D(textureWidth, textureHeight);

        for (var y = 0; y < textureHeight; y++)
        for (var x = 0; x < textureWidth; x++)
        {
            var u = (float)x / textureWidth;
            var v = (float)y / textureHeight;

            // Simple 4-corner gradient
            var c1 = Color.Lerp(color1, color2, u);
            var c2 = Color.Lerp(color3, color4, u);
            _sourceTexture.SetPixel(x, y, Color.Lerp(c1, c2, v));
        }

        _sourceTexture.Apply();
    }

    private void CreatePuzzlePieces()
    {
        var pieceID = 0;
        var pieceWidth = _sourceTexture.width / cols;
        var pieceHeight = _sourceTexture.height / rows;

        for (var r = 0; r < rows; r++)
        for (var c = 0; c < cols; c++)
        {
            // Create Sprite for this piece from the source texture
            // The rect is defined from bottom-left, so we invert Y
            var rect = new Rect(c * pieceWidth, (rows - 1 - r) * pieceHeight, pieceWidth, pieceHeight);
            var pieceImageSprite =
                Sprite.Create(_sourceTexture, rect, new Vector2(0.5f, 0.5f), 100f);

            var pieceGo = Instantiate(puzzlePiecePrefab, scrambleOrigin.transform);
            pieceGo.name = $"Piece_{r}_{c}";
            pieceGo.transform.localScale =
                Vector3.one * (pieceSize / (TextureResolutionPerPiece / 100f));

            // Set the "realistic" shape if available
            var sr = pieceGo.GetComponent<SpriteRenderer>();
            if (sr && puzzlePieceBaseShape)
                // Here, we make the main sprite the base shape, and the image becomes a child,
                // OR we use a shader/material that combines them.
                // For simplicity, let's make the `pieceImageSprite` itself the one that needs to be shaped.
                // This means `Sprite.Create` would ideally create a sprite with a custom mesh outline.
                // Unity's default `Sprite.Create` makes rectangular sprites.
                // To achieve the "realistic" shape:
                // 1. Use Pro version of Sprite Editor for custom physics shape / mesh.
                // 2. Use a masking solution (e.g., SpriteMask or a custom shader).
                // 3. Create pre-cut sprites (most art-intensive).
                // For this example, we'll assign the generated `pieceImageSprite` directly.
                // The `puzzlePiecePrefab` should ideally have the `puzzlePieceBaseShape` as its default sprite.
                // Then we could either:
                //    a) Tint this base shape with the average color of `pieceImageSprite`.
                //    b) Use a shader to map `pieceImageSprite` onto the `puzzlePieceBaseShape`.
                //    c) The easiest: `pieceImageSprite` is the *actual texture*, and the `PuzzlePieceShape.png` is just for visual reference in the prefab.
                //       The actual "shape" of interaction will be the BoxCollider2D.
                //       To make it *look* like a puzzle piece, the `pieceImageSprite` itself should be shaped.
                //       Since Sprite.Create makes rectangles, we'll rely on the prefab's `SpriteRenderer` having the generic puzzle shape
                //       and then apply the image slice to it.
                // Let's assume the prefab's SpriteRenderer already has the puzzle piece shape sprite.
                // We will now apply the generated texture slice to it.
                // This is a bit of a hack if the shape sprite isn't just white.
                // A better way is to have a child sprite for the image.
                // Simple approach: The prefab has the generic shape. We tint it. This is not ideal.
                // sr.sprite = puzzlePieceBaseShape; // This is already set in prefab.
                // sr.color = GetAverageColorFromSprite(pieceImageSprite); // Simple, but loses detail.
                // Better approach: The prefab has a SpriteRenderer which will display the `pieceImageSprite`.
                // If `puzzlePieceBaseShape` defines the visual shape, you'd typically use a Sprite Mask.
                // Let's make `pieceImageSprite` the main visual:
                sr.sprite = pieceImageSprite;
            // To make it *appear* as a puzzle shape, even if `pieceImageSprite` is square,
            // the prefab itself (PuzzlePiece_Prefab) should have the `puzzlePieceBaseShape`
            // as its sprite. Then, `pieceImageSprite` should be applied to a child, or masked.
            // For this example, we're saying the `pieceImageSprite` *is* the final visual.
            // The "realism points" depend on `pieceImageSprite` being actually puzzle-shaped.
            // Since Texture2D -> Sprite.Create makes rectangles, we'll need to use a SpriteMask.
            // Let's try using a SpriteMask on the piece prefab if you want to make square slices *look* like puzzle pieces.
            // 1. PuzzlePiece_Prefab:
            //    - SpriteRenderer (Sprite: `puzzlePieceBaseShape`, Mask Interaction: Visible Outside Mask)
            //    - Child GameObject "ImageHolder":
            //      - SpriteRenderer (Sprite: to be set to `pieceImageSprite`)
            //    - SpriteMask (Sprite: `puzzlePieceBaseShape`)
            // This is more setup. For code, we'll just set the sprite.
            // The visual "puzzle shape" must come from `pieceImageSprite` being pre-shaped or masked.
            var targetPos = boardOrigin.position + new Vector3(c * pieceSize, -r * pieceSize, 0);

            var pieceScript = pieceGo.GetComponent<PuzzlePiece>();
            // Pass the `pieceImageSprite` (the slice of the main image) to the piece
            pieceScript.Initialize(pieceID, targetPos, Vector3.zero, pieceImageSprite, this);

            var outline = Instantiate(puzzlePieceOutlinePrefab, boardOrigin.transform);
            outline.transform.position = targetPos;
            outline.name = $"Outline_{r}_{c}";
            outline.transform.localScale = Vector3.one * (pieceSize / (TextureResolutionPerPiece / 100f));

            _pieces.Add(pieceScript);
            _targetSlots.Add(pieceID, targetPos);
            pieceID++;
        }
    }

    // Helper to get average color (not used in current simple setup, but example)
    private Color GetAverageColorFromSprite(Sprite sprite)
    {
        if (!sprite || !sprite.texture) return Color.white;
        var tex = sprite.texture;
        var r = sprite.rect;
        var pixels = tex.GetPixels((int)r.x, (int)r.y, (int)r.width, (int)r.height);
        var avg = Color.black;
        foreach (var c in pixels) avg += c;
        return avg / pixels.Length;
    }


    private void ShuffleAndPlacePieces()
    {
        var availableScramblePositions = new List<Vector3>();
        var scrambleAreaWidth = cols * pieceSize * 0.8f; // Make scramble area a bit compact
        var scrambleAreaHeight = rows * pieceSize * 0.8f;

        for (var i = 0; i < _pieces.Count; i++)
        {
            // Generate somewhat random positions in the scramble area
            // Ensure they don't overlap too much - simple grid for scramble positions
            var x = i % cols * pieceSize - scrambleAreaWidth / 2f;
            var y = -(i / cols) * pieceSize + scrambleAreaHeight / 2f;
            availableScramblePositions.Add(scrambleOrigin.position + new Vector3(x, y, 0));
        }

        availableScramblePositions.Shuffle();

        for (var i = 0; i < _pieces.Count; i++) _pieces[i].ResetPiece(availableScramblePositions[i]);
    }

    public void PiecePlacedCorrectly(PuzzlePiece piece)
    {
        if (!_correctlyPlacedPieceIDs.Add(piece.pieceID)) return;

        PlaySound(correctPlacementSound);

        if (_correctlyPlacedPieceIDs.Count != _pieces.Count) return;

        _gameActive = false;
        _gameWon = true;
        winText.text = "YOU WIN!\nTime: " + timerText.text[6..];
        winText.gameObject.SetActive(true);
        Debug.Log("Puzzle Solved!");
    }

    public void PiecePlacedIncorrectly()
    {
        PlaySound(incorrectPlacementSound);
    }

    public bool IsSlotTaken(int pieceIdToCheck, Vector3 targetPosition)
    {
        return _pieces.Any(piece =>
            piece.IsPlaced() && piece.pieceID != pieceIdToCheck && piece.transform.position == targetPosition);
    }


    public void PickupPiece()
    {
        // Optional: play a sound when piece is picked up
        // PlaySound(pickupSound);
    }

    private void PlaySound(AudioClip clip)
    {
        if (_audioSource && clip) _audioSource.PlayOneShot(clip);
    }

    public bool IsGameActive()
    {
        return _gameActive;
    }
}