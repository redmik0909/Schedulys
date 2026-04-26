-- Table des licences Schedulys
CREATE TABLE IF NOT EXISTS licenses (
  id              UUID        DEFAULT gen_random_uuid() PRIMARY KEY,
  license_key     TEXT        UNIQUE NOT NULL,
  school_name     TEXT        NOT NULL,
  email           TEXT        NOT NULL DEFAULT '',
  expires_at      DATE        NOT NULL,
  max_activations INTEGER     NOT NULL DEFAULT 2,
  activations     TEXT[]      NOT NULL DEFAULT '{}',
  created_at      TIMESTAMPTZ DEFAULT NOW(),
  revoked         BOOLEAN     NOT NULL DEFAULT FALSE
);

-- Index pour les lookups par clé (le cas d'usage principal)
CREATE INDEX IF NOT EXISTS idx_licenses_key ON licenses(license_key);

-- RLS activé — seule l'Edge Function (service_role) peut lire/écrire
ALTER TABLE licenses ENABLE ROW LEVEL SECURITY;

-- La clé anon ne peut rien faire directement sur cette table
-- Tout passe par la Edge Function qui utilise service_role
