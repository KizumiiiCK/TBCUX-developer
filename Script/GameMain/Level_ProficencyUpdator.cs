using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Level_ProficencyUpdator : MonoBehaviour
{
    private const int count = 13;
    private string[] character_codes = new string[count];
    private CharacterProficiency[] proficiencies = new CharacterProficiency[count];

    /// <summary>
    /// Must be called once when a level/game starts.
    /// Fills local proficiency copies for all deployed characters.
    /// </summary>
    public int[] SetUp(string[] codes)
    {
        int[] levels = new int[count];

        for (int i = 0; i < count; i++)
        {
            character_codes[i] = string.Empty;
            proficiencies[i] = null;
            levels[i] = 0;
        }

        for (int i = 0; i < codes.Length; i++)
        {
            if (i >= count) break;
            try
            {
                character_codes[i] = codes[i].Substring(0, 4);
            }
            catch
            {
                continue;
            }

            // Get proficiency from save
            var details = CharacterUpgradeSave.GetDetails(character_codes[i]);
            proficiencies[i] = details.proficiency;
            //proficiencies[i] = new CharacterProficiency();

            if (proficiencies[i] != null)
                levels[i] = proficiencies[i].level;
        }

        return levels;
    }

    // ============================================================
    // Utility: find the character slot index
    // ============================================================
    private int FindIndex(string code)
    {
        if (string.IsNullOrEmpty(code)) return -1;

        // Extract xxxx
        string sub;
        try
        {
            sub = code.Substring(0, 4);
        }
        catch
        {
            return -1;
        }

        for (int i = 0; i < count; i++)
        {
            if (character_codes[i] == sub)
                return i;
        }
        return -1;
    }

    // ============================================================
    // Recording functions
    // ============================================================

    public void Record_CharacterDeploy(string code)
    {
        int idx = FindIndex(code);
        if (idx < 0 || proficiencies[idx] == null) return;

        proficiencies[idx].AddProgress(0, 1);
    }

    public void Record_CharacterDamageDealt(string code, int dmg)
    {
        int idx = FindIndex(code);
        if (idx < 0 || proficiencies[idx] == null) return;

        proficiencies[idx].AddProgress(1, SafeAbsInt(dmg));
    }

    public void Record_CharacterDamageTaken(string code, int dmg)
    {
        int idx = FindIndex(code);
        if (idx < 0 || proficiencies[idx] == null) return;

        proficiencies[idx].AddProgress(2, SafeAbsInt(dmg));
    }

    public void Record_CharacterDebuffSuffered(string code, int t)
    {
        int idx = FindIndex(code);
        if (idx < 0 || proficiencies[idx] == null) return;

        proficiencies[idx].AddProgress(3, SafeAbsInt(t));
    }

    private static int SafeAbsInt(int value)
    {
        if (value >= 0) return value;
        return value == int.MinValue ? int.MaxValue : -value;
    }

    // ============================================================
    // Final save
    // ============================================================
    public void EndAccounting()
    {
        // Push all accumulated stacks into save
        //for (int i = 0; i < count; i++) proficiencies[i].UpdateLevel();
        CharacterUpgradeSave.BatchUpdateProficiency(character_codes, proficiencies);
    }
}

