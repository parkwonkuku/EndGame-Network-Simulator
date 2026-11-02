// 검토용 재구성: Assets/Project/Scripts/Managers/SaveManager.cs
// 원본의 로컬 저장 버전, 클라우드 응답 처리와 재시도 분기를 유지했습니다.
// 클래스명과 필드를 정리하고 로그 옵션, 데이터 생성, 삭제와 UGS 어댑터 구성을 생략했습니다.

public sealed class SaveManagerCloudSyncExcerpt
{
    private const string SaveKey = "GameData";
    private ISaveStorage localStorage;
    private IAsyncSaveStorage cloudStorage;
    private int localSaveVersion;

    public GameData Data { get; private set; }
    public bool IsCloudSaveEnabled => cloudStorage != null;
    public bool IsCloudLoadCompleted { get; private set; }

    public void Save()
    {
        if (!IsCloudSaveEnabled)
        {
            SaveLocal(markPendingCloudSave: false, out _);
            return;
        }

        SaveLocal(markPendingCloudSave: true, out int version);
        _ = SaveCloudJsonAsync(BuildCloudUploadJson(), version);
    }

    private string SaveLocal(bool markPendingCloudSave, out int version)
    {
        EnsureLocalStorage();
        EnsureData();
        if (markPendingCloudSave)
            Data.hasPendingCloudSave = true;

        string json = JsonUtility.ToJson(Data);
        localStorage.Save(SaveKey, json);
        version = ++localSaveVersion;
        return json;
    }

    private void SaveLocalWithoutVersionChange()
    {
        EnsureLocalStorage();
        EnsureData();
        localStorage.Save(SaveKey, JsonUtility.ToJson(Data));
    }

    private async Task LoadFromCloudAsync(bool overwriteLocal)
    {
        if (!IsCloudSaveEnabled)
        {
            IsCloudLoadCompleted = true;
            return;
        }

        if (Data != null && Data.hasPendingCloudSave)
        {
            await SyncPendingLocalSaveToCloudAsync();
            return;
        }

        try
        {
            if (!await cloudStorage.ExistsAsync(SaveKey))
                return;

            ApplyLoadedJson(await cloudStorage.LoadAsync(SaveKey));

            if (overwriteLocal)
            {
                Data.hasPendingCloudSave = false;
                SaveLocalWithoutVersionChange();
            }

            NotifyDataLoaded();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[SaveManager] Cloud load failed. Local save will be used. {ex.Message}");
        }
        finally
        {
            IsCloudLoadCompleted = true;
        }
    }

    private string BuildCloudUploadJson()
    {
        EnsureData();

        bool pending = Data.hasPendingCloudSave;
        Data.hasPendingCloudSave = false;
        string json = JsonUtility.ToJson(Data);
        Data.hasPendingCloudSave = pending;
        return json;
    }

    private async Task SaveCloudJsonAsync(string json, int version)
    {
        try
        {
            await cloudStorage.SaveAsync(SaveKey, json);
            if (version == localSaveVersion)
            {
                Data.hasPendingCloudSave = false;
                SaveLocalWithoutVersionChange();
            }
        }
        catch (Exception ex)
        {
            if (version == localSaveVersion)
            {
                Data.hasPendingCloudSave = true;
                SaveLocalWithoutVersionChange();
            }

            Debug.LogWarning($"[SaveManager] Cloud save failed. Local save is still available. {ex.Message}");
        }
    }

    private async Task SyncPendingLocalSaveToCloudAsync()
    {
        if (!IsCloudSaveEnabled || Data == null)
        {
            IsCloudLoadCompleted = true;
            return;
        }

        try
        {
            await cloudStorage.SaveAsync(SaveKey, BuildCloudUploadJson());
            Data.hasPendingCloudSave = false;
            SaveLocalWithoutVersionChange();
            NotifyDataLoaded();
        }
        catch (Exception ex)
        {
            Data.hasPendingCloudSave = true;
            SaveLocalWithoutVersionChange();
            Debug.LogWarning($"[SaveManager] Pending local save could not sync. Local save will be retried later. {ex.Message}");
        }
        finally
        {
            IsCloudLoadCompleted = true;
        }
    }

    // EnsureLocalStorage, EnsureData, ApplyLoadedJson, NotifyDataLoaded는 생략했습니다.
}
