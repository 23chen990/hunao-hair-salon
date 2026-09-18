using System;
using System.Collections.Generic;
using System.Reflection;
using HairSalon;
using NUnit.Framework;

/// <summary>
/// The progress-save contract is exercised through reflection so this test file
/// can be staged before the runtime implementation exists.  It also keeps the
/// test storage entirely in memory; no real PlayerPrefs keys are touched.
/// </summary>
public sealed class SalonProgressSaveTests
{
    [SetUp]
    public void ResetMemoryStorage()
    {
        MemoryStorageProxy.Values.Clear();
    }

    [Test]
    public void ProgressDataDefinesVersionedFieldsAndSafeDefaults()
    {
        Type dataType = FindType("SalonProgressData");
        Assert.IsNotNull(dataType, "SalonProgressData must be added before the save tests can pass.");

        AssertField(dataType, "SchemaVersion", typeof(int));
        AssertField(dataType, "DayNumber", typeof(int));
        AssertField(dataType, "Balance", typeof(int));
        AssertField(dataType, "AutoBlowPurchased", typeof(bool));
        AssertField(dataType, "FirstDayComplete", typeof(bool));
        AssertField(dataType, "DaySettled", typeof(bool));
        AssertField(dataType, "ShopSatisfaction", typeof(int));
        AssertField(dataType, "ReputationStars", typeof(float));
        AssertField(dataType, "TutorialCompleted", typeof(bool));
        AssertField(dataType, "CompletedDays", typeof(List<int>));
        AssertField(dataType, "BestCompletedOrders", typeof(List<int>));

        object data = Activator.CreateInstance(dataType);
        Assert.GreaterOrEqual((int)GetField(data, "SchemaVersion"), 1);
        Assert.GreaterOrEqual((int)GetField(data, "DayNumber"), 1);
        Assert.AreEqual(90, GetField(data, "ShopSatisfaction"));
        Assert.GreaterOrEqual((float)GetField(data, "ReputationStars"), 1f);
        Assert.IsNotNull(GetField(data, "CompletedDays"));
        Assert.IsNotNull(GetField(data, "BestCompletedOrders"));
    }

    [Test]
    public void RepositoryRoundTripsDataAndDeletesBothSlots()
    {
        object repository = CreateRepository();
        object expected = CreateData(2, 875, true, true, 4.25f, true, true);
        SetField(expected, "ShopSatisfaction", 84);

        Assert.IsTrue((bool)Invoke(repository, "Save", expected));
        object loaded = Invoke(repository, "Load");
        Assert.IsNotNull(loaded);
        AssertDataEquals(expected, loaded);

        Assert.IsTrue((bool)Invoke(repository, "Delete"));
        Assert.IsFalse(MemoryStorageProxy.Values.ContainsKey(GetKey(repository, "PrimaryKey")));
        Assert.IsFalse(MemoryStorageProxy.Values.ContainsKey(GetKey(repository, "BackupKey")));
        Assert.IsNull(Invoke(repository, "Load"));
    }

    [Test]
    public void RepositoryFallsBackToBackupWhenPrimaryIsCorrupt()
    {
        object repository = CreateRepository();
        object expected = CreateData(2, 875, true, true, 3.5f, true, true);
        object newer = CreateData(3, 1200, true, true, 4f, true, false);
        Assert.IsTrue((bool)Invoke(repository, "Save", expected));
        Assert.IsTrue((bool)Invoke(repository, "Save", newer));

        string primaryKey = GetKey(repository, "PrimaryKey");
        MemoryStorageProxy.Values[primaryKey] = "{ definitely-not-json";

        object loaded = Invoke(repository, "Load");
        Assert.IsNotNull(loaded, "A corrupt primary must fall back to the last valid backup.");
        AssertDataEquals(expected, loaded);
    }

    [Test]
    public void RepositoryRejectsFutureSchemaWithoutOverwritingExistingData()
    {
        object repository = CreateRepository();
        string primaryKey = GetKey(repository, "PrimaryKey");
        string backupKey = GetKey(repository, "BackupKey");
        MemoryStorageProxy.Values[primaryKey] =
            "{\"SchemaVersion\":999,\"DayNumber\":9,\"Balance\":9999}";
        MemoryStorageProxy.Values[backupKey] = string.Empty;

        object currentData = CreateData(2, 300, false, true, 3f, false, false);
        Assert.IsFalse((bool)Invoke(repository, "Save", currentData));
        Assert.AreEqual(
            "{\"SchemaVersion\":999,\"DayNumber\":9,\"Balance\":9999}",
            MemoryStorageProxy.Values[primaryKey]);
        Assert.IsNull(Invoke(repository, "Load"), "A future schema must not be applied by this build.");
    }

    [Test]
    public void RepositoryRejectsOutOfRangeDataWithoutWriting()
    {
        object repository = CreateRepository();
        object invalid = CreateData(0, -1, false, false, 99f, false, false);
        SetField(invalid, "ShopSatisfaction", 101);

        Assert.IsFalse((bool)Invoke(repository, "Save", invalid));
        Assert.IsFalse(MemoryStorageProxy.Values.ContainsKey(GetKey(repository, "PrimaryKey")));
        Assert.IsFalse(MemoryStorageProxy.Values.ContainsKey(GetKey(repository, "BackupKey")));
    }

    [Test]
    public void SchemaOneSaveWithoutSatisfactionDefaultsToNinety()
    {
        object repository = CreateRepository();
        string primaryKey = GetKey(repository, "PrimaryKey");
        MemoryStorageProxy.Values[primaryKey] =
            "{\"SchemaVersion\":1,\"DayNumber\":1,\"Balance\":25," +
            "\"AutoBlowPurchased\":false,\"FirstDayComplete\":false," +
            "\"DaySettled\":false,\"ReputationStars\":3," +
            "\"TutorialCompleted\":false,\"CompletedDays\":[]," +
            "\"BestCompletedOrders\":[]}";

        object loaded = Invoke(repository, "Load");
        Assert.IsNotNull(loaded);
        Assert.AreEqual(90, GetField(loaded, "ShopSatisfaction"));
    }

    private static object CreateRepository()
    {
        Type repositoryType = FindType("PlayerPrefsSalonProgressRepository");
        Assert.IsNotNull(repositoryType,
            "PlayerPrefsSalonProgressRepository must be added before the save tests can pass.");
        Type storageType = FindType("ISalonProgressStorage");
        Assert.IsNotNull(storageType,
            "ISalonProgressStorage must be added before the save tests can pass.");

        object storageProxy = new MemoryStorageProxy();
        ConstructorInfo constructor = repositoryType.GetConstructor(new[] { storageType });
        Assert.IsNotNull(constructor,
            "The repository must expose a constructor accepting ISalonProgressStorage.");
        return constructor.Invoke(new[] { storageProxy });
    }

    private static object CreateData(
        int day,
        int balance,
        bool autoBlow,
        bool firstDayComplete,
        float reputation,
        bool tutorialCompleted,
        bool daySettled)
    {
        Type dataType = FindType("SalonProgressData");
        Assert.IsNotNull(dataType);
        object data = Activator.CreateInstance(dataType);
        SetField(data, "DayNumber", day);
        SetField(data, "Balance", balance);
        SetField(data, "AutoBlowPurchased", autoBlow);
        SetField(data, "FirstDayComplete", firstDayComplete);
        SetField(data, "DaySettled", daySettled);
        SetField(data, "ReputationStars", reputation);
        SetField(data, "TutorialCompleted", tutorialCompleted);
        SetField(data, "CompletedDays", new List<int> { 1 });
        SetField(data, "BestCompletedOrders", new List<int> { 6 });
        return data;
    }

    private static void AssertDataEquals(object expected, object actual)
    {
        string[] fields =
        {
            "SchemaVersion", "DayNumber", "Balance", "AutoBlowPurchased",
            "FirstDayComplete", "DaySettled", "ShopSatisfaction", "ReputationStars",
            "TutorialCompleted",
            "CompletedDays", "BestCompletedOrders"
        };
        Type dataType = expected.GetType();
        for (int i = 0; i < fields.Length; i++)
        {
            object left = GetField(expected, fields[i]);
            object right = GetField(actual, fields[i]);
            if (left is System.Collections.IList leftList && right is System.Collections.IList rightList)
            {
                CollectionAssert.AreEqual(leftList, rightList, fields[i]);
            }
            else if (left is float leftFloat && right is float rightFloat)
            {
                Assert.AreEqual(leftFloat, rightFloat, .0001f, fields[i]);
            }
            else
            {
                Assert.AreEqual(left, right, fields[i]);
            }
        }
    }

    private static string GetKey(object repository, string fieldName)
    {
        FieldInfo field = repository.GetType().GetField(
            fieldName, BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
        Assert.IsNotNull(field, "The repository must expose " + fieldName + " for deterministic storage tests.");
        return (string)field.GetValue(null);
    }

    private static Type FindType(string shortName)
    {
        string fullName = "HairSalon." + shortName;
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (int i = 0; i < assemblies.Length; i++)
        {
            Type type = assemblies[i].GetType(fullName);
            if (type != null) return type;
        }
        return null;
    }

    private static void AssertField(Type type, string name, Type expectedType)
    {
        FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
        Assert.IsNotNull(field, type.Name + " must expose " + name + ".");
        Assert.AreEqual(expectedType, field.FieldType, name + " has the wrong serialized type.");
    }

    private static object GetField(object instance, string name)
    {
        FieldInfo field = instance.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance);
        Assert.IsNotNull(field, "Missing field " + name + ".");
        return field.GetValue(instance);
    }

    private static void SetField(object instance, string name, object value)
    {
        FieldInfo field = instance.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance);
        Assert.IsNotNull(field, "Missing field " + name + ".");
        field.SetValue(instance, value);
    }

    private static object Invoke(object instance, string methodName, params object[] args)
    {
        MethodInfo method = instance.GetType().GetMethod(
            methodName, BindingFlags.Public | BindingFlags.Instance);
        Assert.IsNotNull(method, "Missing repository method " + methodName + ".");
        return method.Invoke(instance, args);
    }

    private sealed class MemoryStorageProxy : ISalonProgressStorage
    {
        public static readonly Dictionary<string, string> Values = new Dictionary<string, string>();

        public bool HasKey(string key)
        {
            return Values.ContainsKey(key);
        }

        public string GetString(string key, string defaultValue)
        {
            return Values.TryGetValue(key, out string value) ? value : defaultValue;
        }

        public void SetString(string key, string value)
        {
            Values[key] = value;
        }

        public void DeleteKey(string key)
        {
            Values.Remove(key);
        }

        public void Save()
        {
        }
    }
}
