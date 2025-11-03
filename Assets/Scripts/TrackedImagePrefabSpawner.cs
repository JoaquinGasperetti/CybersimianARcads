using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

[RequireComponent(typeof(ARTrackedImageManager))]
public class TrackedImagePrefabSpawner : MonoBehaviour
{
    [Header("Lista de prefabs (poner nombre EXACTO igual al nombre en la Reference Image Library)")]
    [Tooltip("Arrastrá tus prefabs 3D aquí. El script usa prefab.name para mapear con el nombre de la imagen.")]
    public List<GameObject> placeablePrefabs;

    // Diccionario: nombreImagen -> prefab
    private Dictionary<string, GameObject> prefabDictionary = new Dictionary<string, GameObject>();

    // Diccionario de instancias activas: trackableId -> instancia
    private Dictionary<string, GameObject> spawnedPrefabs = new Dictionary<string, GameObject>();

    ARTrackedImageManager trackedImageManager;

    void Awake()
    {
        trackedImageManager = GetComponent<ARTrackedImageManager>();

        // Construir diccionario por nombre
        prefabDictionary.Clear();
        foreach (var p in placeablePrefabs)
        {
            if (p != null && !prefabDictionary.ContainsKey(p.name))
                prefabDictionary.Add(p.name, p);
        }
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
        // Agregadas
        foreach (var added in eventArgs.added)
            UpdateImage(added);

        // Actualizadas (pose o estado)
        foreach (var updated in eventArgs.updated)
            UpdateImage(updated);

        // Removidas
        foreach (var removed in eventArgs.removed)
            RemoveImage(removed);
    }

    private void UpdateImage(ARTrackedImage trackedImage)
    {
        var imgName = trackedImage.referenceImage.name; // nombre q pusiste en la Reference Image Library

        // Si la imagen está en estado Tracking -> mostrar / actualizar
        if (trackedImage.trackingState == TrackingState.Tracking)
        {
            // si ya existe instancia para este image (usamos trackableId), la activamos y actualizamos transform
            if (spawnedPrefabs.ContainsKey(trackedImage.referenceImage.name))
            {
                var inst = spawnedPrefabs[trackedImage.referenceImage.name];
                inst.SetActive(true);
                inst.transform.position = trackedImage.transform.position;
                inst.transform.rotation = trackedImage.transform.rotation;
                inst.transform.localScale = Vector3.one; // ajustar si querés escala distinta
            }
            else
            {
                // no existe instancia: buscar prefab por nombre y crearla
                if (prefabDictionary.TryGetValue(imgName, out GameObject prefab))
                {
                    // instanciamos como hijo del trackedImage para seguir su pose
                    var go = Instantiate(prefab, trackedImage.transform);
                    go.transform.localPosition = Vector3.zero;
                    go.transform.localRotation = Quaternion.identity;
                    go.transform.localScale = Vector3.one; // ajustar si hace falta
                    spawnedPrefabs[imgName] = go;
                }
                else
                {
                    Debug.LogWarning($"No encontré prefab para la imagen: {imgName}. Asegurate que el nombre del prefab coincida con el nombre en Reference Image Library.");
                }
            }
        }
        else
        {
            // Estado no tracking (limited / none) -> ocultar la instancia (si existe)
            if (spawnedPrefabs.TryGetValue(imgName, out GameObject inst))
                inst.SetActive(false);
        }
    }

    private void RemoveImage(ARTrackedImage removed)
    {
        var imgName = removed.referenceImage.name;
        if (spawnedPrefabs.TryGetValue(imgName, out GameObject inst))
        {
            Destroy(inst);
            spawnedPrefabs.Remove(imgName);
        }
    }
}
