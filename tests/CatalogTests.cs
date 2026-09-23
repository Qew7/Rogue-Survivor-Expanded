static class CatalogTests
{
    public static void Run()
    {
        object items = Check.Empty("Gameplay.GameItems");
        Check.Equal(true, Check.Call(items, "StartsWithVowel", "apple"), "vowel item name");
        Check.Equal(true, Check.Call(items, "StartsWithVowel", "Yarrow"), "capital Y item name");
        Check.Equal(false, Check.Call(items, "StartsWithVowel", "book"), "consonant item name");
        Check.Equal(true, Check.Call(items, "CheckPlural", "scissors", "scissors"), "same plural");
        Check.Equal(false, Check.Call(items, "CheckPlural", "book", "books"), "different plural");
    }
}
