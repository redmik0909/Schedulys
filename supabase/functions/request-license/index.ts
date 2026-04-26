import { createClient } from "https://esm.sh/@supabase/supabase-js@2";

const supabase = createClient(
  Deno.env.get("SUPABASE_URL")!,
  Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!
);

const NTFY_TOPIC    = Deno.env.get("NTFY_TOPIC")!;          // ex: schedulys-demandes-k9x2m7
const APPROVE_URL   = Deno.env.get("APPROVE_FUNCTION_URL")!; // URL complète de approve-license

const corsHeaders = {
  "Access-Control-Allow-Origin": "*",
  "Content-Type": "application/json",
};

Deno.serve(async (req) => {
  if (req.method === "OPTIONS") {
    return new Response(null, { headers: corsHeaders, status: 204 });
  }

  if (req.method !== "POST") {
    return new Response(JSON.stringify({ error: "Méthode non autorisée." }), {
      headers: corsHeaders, status: 405,
    });
  }

  let body: { school_name?: string; contact_name?: string; email?: string; machine_id?: string };
  try {
    body = await req.json();
  } catch {
    return new Response(JSON.stringify({ error: "Corps invalide." }), {
      headers: corsHeaders, status: 400,
    });
  }

  const { school_name, contact_name, email, machine_id } = body;

  if (!school_name || !contact_name || !email || !machine_id) {
    return new Response(JSON.stringify({ error: "Champs manquants." }), {
      headers: corsHeaders, status: 400,
    });
  }

  // Jeton d'approbation aléatoire (non devinable)
  const approval_token = crypto.randomUUID().replace(/-/g, "");

  const { data, error: dbError } = await supabase
    .from("license_requests")
    .insert({ school_name, contact_name, email, machine_id, approval_token })
    .select("id")
    .single();

  if (dbError || !data) {
    return new Response(JSON.stringify({ error: "Erreur serveur." }), {
      headers: corsHeaders, status: 500,
    });
  }

  const approveLink = `${APPROVE_URL}?request_id=${data.id}&token=${approval_token}`;

  // Notification push ntfy.sh — bouton "Approuver" directement dans la notif
  await fetch(`https://ntfy.sh/${NTFY_TOPIC}`, {
    method: "POST",
    body: `École : ${school_name}\nContact : ${contact_name}\nCourriel : ${email}\nMachine : ${machine_id}`,
    headers: {
      "Title":    `Nouvelle demande — ${school_name}`,
      "Priority": "high",
      "Tags":     "key,school_satchel",
      "Actions":  `http, Approuver, ${approveLink}; http, Voir les demandes, https://supabase.com/dashboard`,
    },
  });

  return new Response(JSON.stringify({ success: true }), { headers: corsHeaders });
});
