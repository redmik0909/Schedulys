import { createClient } from "https://esm.sh/@supabase/supabase-js@2";

const supabase = createClient(
  Deno.env.get("SUPABASE_URL")!,
  Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!
);

Deno.serve(async (req) => {
  const corsHeaders = {
    "Access-Control-Allow-Origin": "*",
    "Content-Type": "application/json",
  };

  if (req.method === "OPTIONS") {
    return new Response(null, { headers: corsHeaders, status: 204 });
  }

  let body: { license_key?: string; machine_id?: string };
  try {
    body = await req.json();
  } catch {
    return new Response(
      JSON.stringify({ valid: false, error: "Corps de requête invalide." }),
      { headers: corsHeaders, status: 400 }
    );
  }

  const { license_key, machine_id } = body;

  if (!license_key || !machine_id) {
    return new Response(
      JSON.stringify({ valid: false, error: "Paramètres manquants." }),
      { headers: corsHeaders, status: 400 }
    );
  }

  const { data: license, error } = await supabase
    .from("licenses")
    .select("*")
    .eq("license_key", license_key.trim().toUpperCase())
    .single();

  if (error || !license) {
    return new Response(
      JSON.stringify({ valid: false, error: "Licence introuvable." }),
      { headers: corsHeaders }
    );
  }

  if (license.revoked) {
    return new Response(
      JSON.stringify({ valid: false, error: "Cette licence a été révoquée." }),
      { headers: corsHeaders }
    );
  }

  if (new Date() > new Date(license.expires_at)) {
    return new Response(
      JSON.stringify({ valid: false, error: "Licence expirée." }),
      { headers: corsHeaders }
    );
  }

  const activations: string[] = license.activations ?? [];

  if (!activations.includes(machine_id)) {
    if (activations.length >= license.max_activations) {
      return new Response(
        JSON.stringify({
          valid: false,
          error: `Nombre maximum d'activations atteint (${license.max_activations}).`,
        }),
        { headers: corsHeaders }
      );
    }

    const { error: updateError } = await supabase
      .from("licenses")
      .update({ activations: [...activations, machine_id] })
      .eq("license_key", license.license_key);

    if (updateError) {
      return new Response(
        JSON.stringify({ valid: false, error: "Erreur lors de l'activation." }),
        { headers: corsHeaders, status: 500 }
      );
    }
  }

  return new Response(
    JSON.stringify({
      valid:       true,
      school_name: license.school_name,
      expires_at:  license.expires_at,
      is_trial:    license.is_trial ?? false,
    }),
    { headers: corsHeaders }
  );
});
