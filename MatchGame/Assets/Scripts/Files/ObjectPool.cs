using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class ObjectPool<T> where T : Component
{
    private readonly Queue<T> _objectPool = new Queue<T>();
    private readonly List<T> _objectList = new();
    private readonly T _prefab;
    private readonly Transform _parent;

    public ObjectPool(T prefab, int initialSize, Transform parent = null)
    {
        _prefab = prefab;
        _parent = parent;
        
        for (int i = 0; i < initialSize; i++)
        {
            CreateNewObject();
        }
    }

    public bool IsPoolEmpty()
    {
        return _objectPool.Count == 0;
    }

    private void CreateNewObject()
    {
        T newObject = Object.Instantiate(_prefab, _parent);
        newObject.gameObject.SetActive(false);
        _objectList.Add(newObject);
        _objectPool.Enqueue(newObject);
    }

    public List<T> GetAllObjects()
    {
        return _objectList;
    }
    public List<Transform> GetAllObjectsTransforms()
    {
        List<Transform> transforms = new List<Transform>();
        foreach (var t in _objectList)
        {
            transforms.Add(t.transform);
        }
        return transforms;
    }
    
    public async UniTask EnsurePoolSizeAsync(int targetSize)
    {
        while (_objectPool.Count < targetSize)
        {
            await CreateNewObjectAsync();
        }
    }
    
    private async UniTask CreateNewObjectAsync()
    {
        await UniTask.Yield();
        CreateNewObject();
    }

    public T Get()
    {
        if (_objectPool.Count == 0)
        {
            CreateNewObject();
        }

        T obj = _objectPool.Dequeue();
        obj.gameObject.SetActive(true);
        return obj;
    }

    public void Return(T obj)
    {
        obj.gameObject.SetActive(false);
        _objectPool.Enqueue(obj);
    }

    public void ReturnAllObjects()
    {
        _objectPool.Clear();

        foreach (var obj in _objectList)
        {
            obj.gameObject.SetActive(false);
            _objectPool.Enqueue(obj);
        }
    }
}