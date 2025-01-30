using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PrimeTween;
using UnityEngine;
using UnityEngine.UI;
using Random = System.Random;

public class Board : MonoBehaviour
{
    [Header("Board Settings")] [SerializeField]
    private LevelData level;

    private int _rowsLength;
    private int _columnsLength;

    [Tooltip("Margin between cells")] [SerializeField]
    float spacing = 0.1f;

    private float _fixedSpacing;

    [Tooltip("The margin of the table to be formed from the right and left axis")] [SerializeField]
    float margin = 0.1f;

    [Header("Assets")] 
    [SerializeField] private RectTransform shuffleButton;
    [SerializeField] private Sprite boardBackground;

    public Tile[,] BoardData { get; private set; }
    private Camera _camera;
    public ObjectPool<Block> BlockPool { get; private set; }

    private readonly Stack<int> _tempStack = new();
    private readonly List<Tile> _matchedTiles = new();
    private bool[] _visitedCells;
    private bool _canShuffle = true;

    #region MonoBehaviour

    void Start()
    {
        _camera = Camera.main;
        CreateBoard();
    }

    private void OnEnable()
    {
        InputHandler.OnTileClicked += CheckTile;
    }

    private void OnDisable()
    {
        InputHandler.OnTileClicked -= CheckTile;
    }

    #endregion

    #region Shuffling

    public async void StartShuffle()
    {
        if(!_canShuffle) return;
        
        foreach (var tile in BoardData)
        {
            if (tile.IsTileFilled)
            {
                tile.Block.CompleteAnimation();
            }
        }
        InputHandler.OnControlInput?.Invoke(false);
        _canShuffle = false;
        
        await SetVisibilityBoardElements(false);

        await ShuffleBoardAsync();
        
        await SetVisibilityBoardElements(true);
        
        CheckBlockGroups(Enumerable.Range(0, _columnsLength).ToList());
        
        InputHandler.OnControlInput?.Invoke(true);
        _canShuffle = true;
    }

    private Tween SetVisibilityShuffleButton(bool visibility)
    {
        return visibility ? UIHelper.MoveOnScreen(shuffleButton.gameObject, Vector2.up,0.4f) : UIHelper.MoveOnScreen(shuffleButton.gameObject,Vector2.down, 0.4f);
    }
    
    private Tween SetVisibilityBoard(bool visibility)
    {
        return visibility ?  UIHelper.MoveOnScreen(transform.gameObject,Vector2.zero, 0.4f) : UIHelper.MoveOnScreen(transform.gameObject, Vector2.right, 0.4f) ;
    }

    private Sequence SetVisibilityBoardElements(bool visibility)
    {
        return Sequence.Create().Group(SetVisibilityShuffleButton(visibility)).Group(SetVisibilityBoard(visibility));
    }

    private async Task ShuffleBoardAsync()
    {
        Random random = new Random();

        List<Block> tempBlocksList = new List<Block>();
        for (int i = 0; i < _rowsLength; i++)
        {
            for (int j = 0; j < _columnsLength; j++)
            {
                tempBlocksList.Add(BoardData[i, j].Block);
            }
        }

        for (int i = tempBlocksList.Count - 1; i > 0; i--)
        {
            int j = random.Next(0, i + 1);
            (tempBlocksList[i], tempBlocksList[j]) = (tempBlocksList[j], tempBlocksList[i]);
        }

        int index = 0;
        for (int i = 0; i < _rowsLength; i++)
        {
            for (int j = 0; j < _columnsLength; j++)
            {
                BoardData[i, j].MarkAsEmpty();
                BoardData[i, j].AssignBlock(tempBlocksList[index++], true);
                await Task.Yield();
            }
        }
    }
    
    private bool IsBoardPlayable()
    {
        foreach (var tile in BoardData)
        {
            if (!tile.IsTileFilled)
                continue;

            if (HasMatchingNeighbors(tile))
                return true;
        }

        return false;
    }

    private bool HasMatchingNeighbors(Tile tile)
    {
        var directions = new (int rowOffset, int colOffset)[]
        {
            (-1, 0),
            (1, 0),
            (0, -1),
            (0, 1)
        };

        BlockColor targetColor = tile.Block.Data.BlockColor;

        foreach (var (rowOffset, colOffset) in directions)
        {
            int newRow = tile.Row + rowOffset;
            int newColumn = tile.Column + colOffset;

            if (!IsOutOfBounds(newRow, newColumn))
            {
                Tile neighbor = BoardData[newRow, newColumn];
                if (neighbor.IsTileFilled && neighbor.Block.Data.BlockColor == targetColor)
                    return true;
            }
        }

        return false;
    }

    #endregion

    #region Checks

    private void CheckTile(Tile tile)
    {
        if (tile.Block != null)
        {
            var matchedTiles = FloodFill(tile);
            if (matchedTiles.Count < 2)
            {
                BlockManager.Instance.ShakeBlock(tile.Block);
                return;
            }

            foreach (var element in matchedTiles)
            {
                element.RemoveBlock();
            }

            CheckRows(matchedTiles);
        }
    }

    private void CheckRows(List<Tile> tiles)
    {
        var fillingColumns = new List<int>();
        foreach (var tile in tiles)
        {
            if (fillingColumns.Contains(tile.Column)) continue;
            fillingColumns.Add(tile.Column);
        }

        FillColumns(fillingColumns);
    }

    private void CheckBlockGroups(List<int> columns)
    {
        //Ekstra sağ ve sol sütunu kontrol etmek için.
        columns.Add(columns[^1] + 1);
        columns.Add(columns[0] - 1);
        

        foreach (var column in columns)
        {
            for (int i = 0; i < _rowsLength; i++)
            {
                if (IsOutOfBounds(i, column)) continue;
                var matchedTiles = FloodFill(BoardData[i, column]);
                foreach (var tile in matchedTiles)
                {
                    BlockManager.Instance.SetBlockType(tile.Block, matchedTiles.Count);
                }
            }
        }
    }

    #endregion

    #region Filling

    private void OrderColumn(int columnNum)
    {
        for (int row = _rowsLength - 1; row >= 0; row--) // Sütunun en altından başlayarak yukarı çık
        {
            Tile currentTile = BoardData[row, columnNum];
            if (currentTile.IsTileFilled) continue; // Eğer boş bir kutu bulduysak

            for (int upperRow = row - 1; upperRow >= 0; upperRow--) // Daha üst sıralardan dolu bir kutu ara
            {
                Tile upperTile = BoardData[upperRow, columnNum];
                
                if (!upperTile.IsTileFilled) continue; // Eğer dolu bir kutu bulursak
                BlockManager.Instance.MoveBlock(upperTile, currentTile);
                break;
            }
        }
    }
    
    private void FillColumns(List<int> columns)
    {
        foreach (var column in columns)
        {
            OrderColumn(column);
            BlockManager.Instance.SpawnBlock(column);
        }
        
        CheckBlockGroups(columns);

        if (IsBoardPlayable()) return;
        StartShuffle();

    }

    void CreateBoard()
    {
        if (level == null)
        {
            _rowsLength = 10;
            _columnsLength = 9;
        }
        else
        {
            _rowsLength = level.Row;
            _columnsLength = level.Column;
        }
        
        
        var tempTileObject = new GameObject().AddComponent<Tile>();
        var collider = tempTileObject.gameObject.AddComponent<BoxCollider2D>();
        BlockManager.Instance.SetBlockSize(collider.bounds.size.y);
        BoardData = new Tile[_rowsLength, _columnsLength];

        float height = 2f * _camera.orthographicSize;
        float width = height * _camera.aspect;
        _fixedSpacing = (_camera.aspect / -6.5f) * spacing;

        float maxCellWidth = (width - 2 * margin - (_columnsLength - 1) * _fixedSpacing) / _columnsLength;
        float maxCellHeight = (height - 2 * margin - (_rowsLength - 1) * _fixedSpacing) / _rowsLength;

        float cellSize = Mathf.Min(maxCellWidth, maxCellHeight);

        Vector3 cellScale = new Vector3(cellSize*1.17f, cellSize*1.17f, 1);
        tempTileObject.transform.localScale = cellScale;

        var tempBlockSpriteObject = new GameObject().AddComponent<Block>();
        var spriteRenderer = tempBlockSpriteObject.gameObject.AddComponent<SpriteRenderer>();
        tempBlockSpriteObject.transform.localScale = cellScale;
        
        BlockPool = new ObjectPool<Block>(tempBlockSpriteObject, _rowsLength * _columnsLength, transform);

        Vector3 startPosition = new Vector3(-((_columnsLength - 1) * (cellSize + _fixedSpacing)) / 2,
            ((_rowsLength - 1) * (cellSize + _fixedSpacing)) / 2, 0);
        
        int index = 0;
        for (int i = 0; i < _rowsLength; i++)
        {
            for (int j = 0; j < _columnsLength; j++)
            {
                Vector3 position = startPosition + new Vector3(j * (cellSize + _fixedSpacing),
                    -i * (cellSize + _fixedSpacing), 0);
                BoardData[i, j] = Instantiate(tempTileObject, position, Quaternion.identity, transform);
                BoardData[i, j].Init(i, j,
                    level == null
                        ? BlockManager.Instance.GetRandomBlock()
                        : BlockManager.Instance.GetBlock(level.Board[j, i].BlockColor));
                index++;
            }
        }
        
        CreateBoardBackground(startPosition, cellSize);

        CheckBlockGroups(Enumerable.Range(0, _columnsLength).ToList());
        
        Destroy(tempTileObject);
        Destroy(tempBlockSpriteObject);
        
        SetVisibilityShuffleButton(true);
    }
    
    private void CreateBoardBackground(Vector3 startPosition, float cellSize)
    {
        if (boardBackground == null) return;

        // Board'un gerçek genişlik ve yüksekliğini hesapla
        float totalWidth = (_columnsLength * cellSize) + ((_columnsLength - 1) * _fixedSpacing);
        float totalHeight = (_rowsLength * cellSize) + ((_rowsLength - 1) * _fixedSpacing);

        // Arka plan nesnesini oluştur
        GameObject backgroundObject = new GameObject("BoardBackground");
        backgroundObject.transform.SetParent(transform);

        SpriteRenderer renderer = backgroundObject.AddComponent<SpriteRenderer>();
        renderer.sprite = boardBackground;
        renderer.sortingOrder = -11; // Arkada kalması için

        // Sprite'ı board'un boyutuna ölçekle
        backgroundObject.transform.localScale = new Vector3((totalWidth / renderer.sprite.bounds.size.x) + 0.05f,
            (totalHeight / renderer.sprite.bounds.size.y )+0.05f, 1) ;

        // Board'un ortasına hizala
        backgroundObject.transform.position = startPosition + new Vector3(totalWidth / 2 - cellSize / 2, 
            -totalHeight / 2 + cellSize / 2, 0);
    
    }

    public List<Tile> FloodFill(Tile startTile)
    {
        InitializeFloodFill();

        int startRow = startTile.Row;
        int startColumn = startTile.Column;
        BlockColor targetColor = startTile.Block.Data.BlockColor;

        _tempStack.Push(GetIndex(startRow, startColumn, _columnsLength));

        _matchedTiles.Clear();

        while (_tempStack.Count > 0)
        {
            int index = _tempStack.Pop();
            int row = index / _columnsLength;
            int column = index % _columnsLength;

            if (IsOutOfBounds(row, column) || _visitedCells[index])
                continue;

            Tile currentTile = BoardData[row, column];
            if (!currentTile.IsTileFilled || currentTile.Block.Data.BlockColor != targetColor) continue;

            _visitedCells[index] = true;
            _matchedTiles.Add(currentTile);

            AddTilesToStack(row, column);
        }

        return _matchedTiles;
    }

    private void InitializeFloodFill()
    {
        int totalCells = _rowsLength * _columnsLength;

        if (_visitedCells == null || _visitedCells.Length < totalCells)
            _visitedCells = new bool[totalCells];
        else
            Array.Clear(_visitedCells, 0, totalCells);
    }
    
    private void AddTilesToStack(int row, int column)
    {
        var directions = new (int rowOffset, int colOffset)[]
        {
            (-1, 0),
            (1, 0),
            (0, -1),
            (0, 1)
        };

        foreach (var (rowOffset, colOffset) in directions)
        {
            int newRow = row + rowOffset;
            int newColumn = column + colOffset;

            if (!IsOutOfBounds(newRow, newColumn))
            {
                _tempStack.Push(GetIndex(newRow, newColumn, _columnsLength));
            }
        }
    }

    #endregion

    #region Helpers

    private bool IsOutOfBounds(int row, int column)
    {
        return row < 0 || row >= _rowsLength || column < 0 || column >= _columnsLength;
    }

    private int GetIndex(int row, int column, int columnCount)
    {
        return row * columnCount + column;
    }
    
    public int GetEmptyTileCountOnColumn(int columnNum)
    {
        int emptyTileAmount = 0;
        for (int row = _rowsLength - 1; row >= 0; row--)
        {
            Tile currentTile = BoardData[row, columnNum];
            if (currentTile.IsTileFilled) continue;
            emptyTileAmount++;
        }

        return emptyTileAmount;
    }

    #endregion
    


}