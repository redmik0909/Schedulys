-- Table des demandes de licence (utilisateurs sans clé)
CREATE TABLE IF NOT EXISTS license_requests (
  id             UUID        DEFAULT gen_random_uuid() PRIMARY KEY,
  school_name    TEXT        NOT NULL,
  contact_name   TEXT        NOT NULL,
  email          TEXT        NOT NULL,
  machine_id     TEXT        NOT NULL,
  approval_token TEXT        NOT NULL,
  status         TEXT        NOT NULL DEFAULT 'pending',  -- pending | approved | rejected
  license_key    TEXT,
  created_at     TIMESTAMPTZ DEFAULT NOW(),
  approved_at    TIMESTAMPTZ
);

CREATE INDEX IF NOT EXISTS idx_license_requests_status ON license_requests(status);

-- RLS activé — tout passe par les Edge Functions (service_role)
ALTER TABLE license_requests ENABLE ROW LEVEL SECURITY;
