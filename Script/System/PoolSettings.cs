using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
public class Pool
{
    public string pool_name;
    public int pool_start_delay;
    public int pool_cycle_period;
    public int pool_duration;
    public bool gold_capsule;
    public RewardName[] cost_item = new RewardName[2];
    public int[] cost_amount;
    public int[] draw_times;
    public float[] dropRates=new float[6];
    public List<int[]> dropUnits;
    public bool IsPoolActivating() => PoolSystemTime.IsActivityActive(pool_start_delay, pool_cycle_period, pool_duration);
    public Sprite GetPoolIcon() => Resources.Load<Sprite>($"Pools/icon/{pool_name}");
    public Sprite GetPoolPoster() => Resources.Load<Sprite>($"Pools/poster/{pool_name}");
    public int Draw(bool bonus=false)
    {
        float full_dr = dropRates.Sum(); if (bonus) full_dr -= dropRates[2];
        float r = UnityEngine.Random.Range(0, full_dr);
        float stack_dr = 0;
        int rality = 0;
        for (int i = 0; i < 7; i++) {
            if (bonus && i == 2) continue;
            stack_dr += dropRates[i];
            if (stack_dr > r)
            {
                rality = i;
                break;
            }
        }
        int code = dropUnits[rality][UnityEngine.Random.Range(0, dropUnits[rality].Length)];
        int result = rality * 1000 + code;
        Debug.Log(result);
        return result;
    }
}

public static class PoolSystemTime
{
    public static readonly DateTime openTime = new DateTime(year: 2026, month: 4, day: 2);
    public static DateTime Now => DateTime.Now;

    public static int DaysBetween(DateTime d1, DateTime d2) =>
        Math.Abs((d1.Date - d2.Date).Days);

    public static (int year, int month, int day) GetYMD(DateTime dt) =>
        (dt.Year, dt.Month, dt.Day);

    public static bool IsActivityActive(int firstDelayDays, int cycleDays, int durationDays)
    {
        if (cycleDays <= 0 || durationDays <= 0) return true;

        DateTime now = DateTime.Now.Date;
        int totalDays = (int)(now - openTime.Date).TotalDays;

        if (totalDays < firstDelayDays) return false;

        int daysSinceFirst = totalDays - firstDelayDays;
        int daysInCycle = daysSinceFirst % cycleDays;

        return daysInCycle >= 0 && daysInCycle < durationDays;
    }
    public static int ActivityDayLeft(int firstDelayDays, int cycleDays, int durationDays)
    {
        DateTime now = DateTime.Now.Date;
        int totalDays = (int)(now - openTime.Date).TotalDays;

        int daysSinceFirst = totalDays - firstDelayDays;
        int daysInCycle = daysSinceFirst % cycleDays;

        return durationDays-daysInCycle;
    }
}

public static class PoolInfo
{
    //
    public static readonly bool test_free = false;//
    private const int cycle_DEFAULT=20;
    private const int duration_DEFAULT=4;
    private static readonly float[] droprate_DEFAULT = new float[7] { 0, 0, 0.695f, 0.25f, 0.05f, 0.005f,0 };
    //
    private static readonly int[] Regular_Rares = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24 };
    private static readonly int[] Regular_Superrares = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16};
    public static readonly Pool[] pools =
    {
        new Pool()
        {
            pool_name="darkheros",
            pool_start_delay = 0,
            pool_cycle_period=cycle_DEFAULT,
            pool_duration=duration_DEFAULT,
            gold_capsule=true,
            cost_item=new RewardName[2]{ RewardName.Ticket_Gold, RewardName.CANs},
            cost_amount=new int[2]{ 1, 1500 },
            draw_times=new int[2]{ 1, 11},
            dropRates=droprate_DEFAULT,
            dropUnits=new List<int[]>()
            {
                /* N     */null,
                /* EX    */null,
                /* R     */Regular_Rares,
                /* SR    */Regular_Superrares,
                /* UR    */new int[] {0,1,2,3,/*4,*/5,6,/*7,*/8,9/*,10*/},
                /* LR    */new int[] {6 },
                /* G     */null
            }
        }, // Dark Heros
        new Pool()
        {
            pool_name="unknown",
            pool_start_delay = 0,
            pool_cycle_period=cycle_DEFAULT,
            pool_duration=duration_DEFAULT,
            gold_capsule=true,
            cost_item=new RewardName[2]{ RewardName.Ticket_Gold, RewardName.CANs},
            cost_amount=new int[2]{ 1, 1500 },
            draw_times=new int[2]{ 1, 11},
            dropRates=droprate_DEFAULT,
            dropUnits=new List<int[]>()
            {
                /* N     */null,
                /* EX    */null,
                /* R     */Regular_Rares,
                /* SR    */Regular_Superrares,
                /* UR    */new int[] {15,16,17,18,19,20,21,22,23/*,24*/,25 },
                /* LR    */new int[] {5 },
                /* G     */null
            }
        }, // Unknown Cats
        new Pool()
        {
            pool_name="dragon",
            pool_start_delay = duration_DEFAULT,
            pool_cycle_period=cycle_DEFAULT,
            pool_duration=duration_DEFAULT,
            gold_capsule=true,
            cost_item=new RewardName[2]{ RewardName.Ticket_Gold, RewardName.CANs},
            cost_amount=new int[2]{ 1, 1500 },
            draw_times=new int[2]{ 1, 11},
            dropRates=droprate_DEFAULT,
            dropUnits=new List<int[]>()
            {
                /* N     */null,
                /* EX    */null,
                /* R     */Regular_Rares,
                /* SR    */Regular_Superrares,
                /* UR    */new int[] {30,31,32,33,34,35,36,37,38,39/*,40*/ },
                /* LR    */new int[] {3 },
                /* G     */null
            }
        }, // Dragon
        new Pool()
        {
            pool_name="ancient",
            pool_start_delay = duration_DEFAULT,
            pool_cycle_period=cycle_DEFAULT,
            pool_duration=duration_DEFAULT,
            gold_capsule=true,
            cost_item=new RewardName[2]{ RewardName.Ticket_Gold, RewardName.CANs},
            cost_amount=new int[2]{ 1, 1500 },
            draw_times=new int[2]{ 1, 11},
            dropRates=droprate_DEFAULT,
            dropUnits=new List<int[]>()
            {
                /* N     */null,
                /* EX    */null,
                /* R     */Regular_Rares,
                /* SR    */Regular_Superrares,
                /* UR    */new int[] {45,46,47,48,49,50,51,52,53,54/*,55*/ },
                /* LR    */new int[] {4 },
                /* G     */null
            }
        }, // Ancient Heros

        new Pool()
        {
            pool_name="galaxygals",
            pool_start_delay = duration_DEFAULT * 2,
            pool_cycle_period=cycle_DEFAULT,
            pool_duration=duration_DEFAULT,
            gold_capsule=true,
            cost_item=new RewardName[2]{ RewardName.Ticket_Gold, RewardName.CANs},
            cost_amount=new int[2]{ 1, 1500 },
            draw_times=new int[2]{ 1, 11},
            dropRates=droprate_DEFAULT,
            dropUnits=new List<int[]>()
            {
                /* N     */null,
                /* EX    */null,
                /* R     */Regular_Rares,
                /* SR    */Regular_Superrares,
                /* UR    */new int[] {60,61,62,63,64,65,66,67,/*68,*/69/*,70*/ },
                /* LR    */new int[] {2 },
                /* G     */null
            }
        }, // Galaxygals

        new Pool()
        {
            pool_name="almighties",
            pool_start_delay = duration_DEFAULT * 2,
            pool_cycle_period=cycle_DEFAULT,
            pool_duration=duration_DEFAULT,
            gold_capsule=true,
            cost_item=new RewardName[2]{ RewardName.Ticket_Gold, RewardName.CANs},
            cost_amount=new int[2]{ 1, 1500 },
            draw_times=new int[2]{ 1, 11},
            dropRates=droprate_DEFAULT,
            dropUnits=new List<int[]>()
            {
                /* N     */null,
                /* EX    */null,
                /* R     */Regular_Rares,
                /* SR    */Regular_Superrares,
                /* UR    */new int[] {75,76,77,78,79,80,81,82,83,84 },
                /* LR    */new int[] {7 },
                /* G     */null
            }
        }, // Almighties

        new Pool()
        {
            pool_name="sengoku",
            pool_start_delay = duration_DEFAULT * 3,
            pool_cycle_period=cycle_DEFAULT,
            pool_duration=duration_DEFAULT,
            gold_capsule=true,
            cost_item=new RewardName[2]{ RewardName.Ticket_Gold, RewardName.CANs},
            cost_amount=new int[2]{ 1, 1500 },
            draw_times=new int[2]{ 1, 11},
            dropRates=droprate_DEFAULT,
            dropUnits=new List<int[]>()
            {
                /* N     */null,
                /* EX    */null,
                /* R     */Regular_Rares,
                /* SR    */Regular_Superrares,
                /* UR    */new int[] {90,91,92,93,/*94,*/95,96,97,98,99/*,100*/ },
                /* LR    */new int[] {1 },
                /* G     */null
            }
        }, // Sengoku

        new Pool()
        {
            pool_name="dynamits",
            pool_start_delay = duration_DEFAULT * 3,
            pool_cycle_period=cycle_DEFAULT,
            pool_duration=duration_DEFAULT,
            gold_capsule=true,
            cost_item=new RewardName[2]{ RewardName.Ticket_Gold, RewardName.CANs},
            cost_amount=new int[2]{ 1, 1500 },
            draw_times=new int[2]{ 1, 11},
            dropRates=droprate_DEFAULT,
            dropUnits=new List<int[]>()
            {
                /* N     */null,
                /* EX    */null,
                /* R     */Regular_Rares,
                /* SR    */Regular_Superrares,
                /* UR    */new int[] {105,106,107,108,109,110,111,112,113,114 },
                /* LR    */new int[] {0 },
                /* G     */null
            }
        }, // Dynamits

        new Pool()
        {
            pool_name="platinum",
            pool_start_delay = 0,
            pool_cycle_period=-1,
            pool_duration=duration_DEFAULT,
            gold_capsule=true,
            cost_item=new RewardName[2]{ RewardName.Ticket_Platinum, RewardName.Ticket_Platinum},
            cost_amount=new int[2]{ 1, 10 },
            draw_times=new int[2]{ 1, 10},
            dropRates=new float[7]{ 0,0,0,0,0.95f,0.05f,0},
            dropUnits=new List<int[]>()
            {
                /* N     */null,
                /* EX    */null,
                /* R     */null,
                /* SR    */null,
                /* UR    */new int[] {
                               0, 1, 2, 3,/*4,*/5, 6,/*7,*/8, 9/*,10*/,
                               15,16,17,18,19,20,21,22,23/*,24*/,25,
                               30,31,32,33,34,35,36,37,38,39/*,40*/,
                               45,46,47,48,49,50,51,52,53,54/*,55*/,
                               60,61,62,63,64,65,66,67,/*68,*/69/*,70*/,
                               75,76,77,78,79,80,81,82,83,84,
                               90,91,92,93,/*94,*/95,96,97,98,99,/*100,*/
                               105,106,107,108,109,110,111,112,113,114},
                /* LR    */new int[] {0,1,2,3,4,5,6,7 },
                /* G     */null
            }
        }, // Platinum
    };

}
