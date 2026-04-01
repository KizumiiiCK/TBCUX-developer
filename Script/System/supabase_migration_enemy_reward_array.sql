-- One-row-per-player schema for enemy meet and reward inventory.
-- Run in Supabase SQL editor.

BEGIN;

-- Replace enemy_meet: from (pid, enemy_code, met) multi-row
-- to single row: (pid, met_flags boolean[] length 1000)
DROP TABLE IF EXISTS public.enemy_meet;
CREATE TABLE public.enemy_meet (
  pid character(8) NOT NULL,
  met_flags boolean[] NOT NULL DEFAULT array_fill(false, ARRAY[1000]),
  CONSTRAINT enemy_meet_pkey PRIMARY KEY (pid)
) TABLESPACE pg_default;

-- Replace reward_inventory: from (pid, reward_name, amount) multi-row
-- to single row: (pid, amounts integer[])
DROP TABLE IF EXISTS public.reward_inventory;
CREATE TABLE public.reward_inventory (
  pid character(8) NOT NULL,
  amounts integer[] NOT NULL DEFAULT '{}'::integer[],
  CONSTRAINT reward_inventory_pkey PRIMARY KEY (pid)
) TABLESPACE pg_default;

COMMIT;
