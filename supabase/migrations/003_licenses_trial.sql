-- Ajout du support des licences d'essai
ALTER TABLE licenses ADD COLUMN IF NOT EXISTS is_trial BOOLEAN NOT NULL DEFAULT false;
