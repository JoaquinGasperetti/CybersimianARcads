using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

[RequireComponent(typeof(ARTrackedImageManager))]
public class TrackedImagePrefabSpawner_Scaled : MonoBehaviour
{
    [Header("Prefabs (el nombre del prefab debe coincidir con el nombre en la Reference Image Library)")]
    public List<GameObject> placeablePrefabs;

    [Header("Ajustes de escala")]
    [Tooltip("Multiplica la escala calculada. Útil para agrandar/reducir globalmente (1 = exacto al tamaño de la carta).")]
    public float scaleMultiplier = 1f;

    [Tooltip("Clamp mínimo de escala para evitar valores 0 o extremadamente pequeños.")]
    public float minScale = 0.001f;

    [Tooltip("Clamp máximo de escala para seguridad.")]
    public float maxScale = 10f;

    // Diccionarios para mapping y control de instancias
    private Dictionary<string, GameObject> prefabDictionary = new Dictionary<string, GameObject>();
    private Dictionary<string, GameObject> spawnedPrefabs = new Dictionary<string, GameObject>();

    ARTrackedImageManager trackedImageManager;

    void Awake()
    {
        trackedImageManager = GetComponent<ARTrackedImageManager>();
        prefabDictionary.Clear();
        foreach (var p in placeablePrefabs)
            if (p != null && !prefabDictionary.ContainsKey(p.name))
                prefabDictionary.Add(p.name, p);
    }

    void OnEnable()
    {
        trackedImageManager.trackedImagesChanged += OnTrackedImagesChanged;
    }

    void OnDisable()
    {
        trackedImageManager.trackedImagesChanged -= OnTrackedImagesChanged;
    }

    private void OnTrackedImagesChanged(ARTrackedImagesChangedEventArgs eventArgs)
    {
        foreach (var added in eventArgs.added)
            UpdateImage(added);

        foreach (var updated in eventArgs.updated)
            UpdateImage(updated);

        foreach (var removed in eventArgs.removed)
            RemoveImage(removed);
    }

    private void UpdateImage(ARTrackedImage trackedImage)
    {
        string imgName = trackedImage.referenceImage.name;

        if (trackedImage.trackingState == TrackingState.Tracking)
        {
            // Si ya existe, activarla y actualizar pose/escala (por si la carta cambia de tamaño)
            if (spawnedPrefabs.TryGetValue(imgName, out GameObject existing))
            {
                existing.SetActive(true);
                existing.transform.position = trackedImage.transform.position;
                existing.transform.rotation = trackedImage.transform.rotation;

                // Reajustar escala en caso de que la imagen tenga size diferente
                AdjustScaleAndPositionToImage(existing, trackedImage);
            }
            else
            {
                if (prefabDictionary.TryGetValue(imgName, out GameObject prefab))
                {
                    // Instanciamos como hijo del trackedImage
                    var go = Instantiate(prefab, trackedImage.transform);
                    go.transform.localPosition = Vector3.zero;
                    go.transform.localRotation = Quaternion.identity;

                    // Normalizamos scale temporalmente para medir dimensiones reales del modelo
                    go.transform.localScale = Vector3.one;

                    // Calculamos y aplicamos la escala para que quepa en la carta
                    AdjustScaleAndPositionToImage(go, trackedImage);

                    spawnedPrefabs[imgName] = go;
                }
                else
                {
                    Debug.LogWarning($"No encontré prefab para la imagen: {imgName}. Asegurate que el prefab tenga el mismo nombre que la imagen en la Reference Image Library.");
                }
            }
        }
        else
        {
            if (spawnedPrefabs.TryGetValue(imgName, out GameObject inst))
                inst.SetActive(false);
        }
    }

    private void RemoveImage(ARTrackedImage removed)
    {
        string imgName = removed.referenceImage.name;
        if (spawnedPrefabs.TryGetValue(imgName, out GameObject inst))
        {
            Destroy(inst);
            spawnedPrefabs.Remove(imgName);
        }
    }

    private void AdjustScaleAndPositionToImage(GameObject go, ARTrackedImage trackedImage)
    {
        // 1) calcular bounding box combinado (world space)
        Bounds combinedBounds = new Bounds();
        bool hasBounds = false;
        var renderers = go.GetComponentsInChildren<Renderer>();
        foreach (var r in renderers)
        {
            if (!hasBounds)
            {
                combinedBounds = r.bounds;
                hasBounds = true;
            }
            else
            {
                combinedBounds.Encapsulate(r.bounds);
            }
        }

        if (!hasBounds)
        {
            // Si no tiene renderers, salimos (no hay forma de escalar automáticamente)
            Debug.LogWarning($"Prefab '{go.name}' no tiene Renderers para calcular bounds. Dejalo con su escala por defecto.");
            return;
        }

        // 2) tomar la dimensión máxima del modelo (en metros, world space)
        float modelMaxSize = Mathf.Max(combinedBounds.size.x, combinedBounds.size.y, combinedBounds.size.z);
        if (modelMaxSize <= 0f) modelMaxSize = 0.001f; // seguridad

        // 3) tamaño objetivo según la imagen (usar el mayor de ancho/alto para mantener proporción)
        float imageSize = Mathf.Max(trackedImage.size.x, trackedImage.size.y); // la Reference Image Library define estas medidas (ej 0.07)
        if (imageSize <= 0f) imageSize = 0.07f; // fallback

        // 4) factor de escala
        float scaleFactor = (imageSize / modelMaxSize) * scaleMultiplier;
        scaleFactor = Mathf.Clamp(scaleFactor, minScale, maxScale);

        // Aplicamos la escala (localScale porque el objeto es hijo del trackable)
        go.transform.localScale = Vector3.one * scaleFactor;

        // 5) Recalcular bounds después de escalar (los bounds de los renderers ya reflejarán la escala)
        Bounds scaledBounds = new Bounds();
        bool hasScaled = false;
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            if (!hasScaled)
            {
                scaledBounds = r.bounds;
                hasScaled = true;
            }
            else
            {
                scaledBounds.Encapsulate(r.bounds);
            }
        }

        // 6) Ajustar posición vertical para que la base (min.y) del modelo quede apoyada sobre la carta (la carta está en trackedImage.transform.position)
        // deltaY = (altura del plano de la carta) - bounds.min.y (en coordenadas world)
        float deltaY = trackedImage.transform.position.y - scaledBounds.min.y;
        go.transform.position += new Vector3(0f, deltaY, 0f);
    }
}
