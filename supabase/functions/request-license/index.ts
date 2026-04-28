import { createClient } from "https://esm.sh/@supabase/supabase-js@2";

const supabase = createClient(
  Deno.env.get("SUPABASE_URL")!,
  Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!
);

const RESEND_API_KEY  = Deno.env.get("RESEND_API_KEY")!;
const FROM_EMAIL      = Deno.env.get("FROM_EMAIL") ?? "Schedulys <noreply@revolvittech.com>";
const ADMIN_EMAIL     = Deno.env.get("ADMIN_EMAIL") ?? "redarahal0909@gmail.com";
const APPROVE_URL     = Deno.env.get("APPROVE_FUNCTION_URL")!;

const corsHeaders = {
  "Access-Control-Allow-Origin": "*",
  "Content-Type": "application/json",
};

function htmlAdminNotif(school: string, contact: string, email: string, machine: string, approveLink: string, trialLink: string): string {
  return `<!DOCTYPE html><html><body style="font-family:sans-serif;max-width:560px;margin:0 auto">
    <h2 style="color:#4F46E5">🔑 Nouvelle demande de licence</h2>
    <table style="width:100%;border-collapse:collapse;margin:16px 0">
      <tr><td style="padding:8px;color:#64748B;width:140px">École</td><td style="padding:8px;font-weight:600">${school}</td></tr>
      <tr style="background:#F8FAFC"><td style="padding:8px;color:#64748B">Contact</td><td style="padding:8px">${contact}</td></tr>
      <tr><td style="padding:8px;color:#64748B">Courriel</td><td style="padding:8px">${email}</td></tr>
      <tr style="background:#F8FAFC"><td style="padding:8px;color:#64748B">Machine ID</td><td style="padding:8px;font-family:Consolas">${machine}</td></tr>
    </table>
    <div style="text-align:center;margin:32px 0;display:flex;gap:16px;justify-content:center">
      <a href="${approveLink}"
         style="background:#4F46E5;color:white;text-decoration:none;padding:14px 28px;border-radius:8px;font-size:15px;font-weight:600;display:inline-block">
        ✅ Approuver (1 an)
      </a>
      <a href="${trialLink}"
         style="background:#0EA5E9;color:white;text-decoration:none;padding:14px 28px;border-radius:8px;font-size:15px;font-weight:600;display:inline-block">
        🔬 Essai 30 jours
      </a>
    </div>
    <hr style="margin:32px 0;border:none;border-top:1px solid #E2E8F0"/>
    <p style="font-size:11px;color:#94A3B8">Revolvit Technologies · Schedulys Admin</p>
  </body></html>`;
}

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
  const trialLink   = `${APPROVE_URL}?request_id=${data.id}&token=${approval_token}&trial=true`;

  // Email à l'admin via Resend
  const resendResp = await fetch("https://api.resend.com/emails", {
    method: "POST",
    headers: {
      "Authorization": `Bearer ${RESEND_API_KEY}`,
      "Content-Type":  "application/json",
    },
    body: JSON.stringify({
      from:    FROM_EMAIL,
      to:      ADMIN_EMAIL,
      subject: `🔑 Nouvelle demande de licence — ${school_name}`,
      html:    htmlAdminNotif(school_name, contact_name, email, machine_id, approveLink, trialLink),
    }),
  });

  if (!resendResp.ok) {
    const resendErr = await resendResp.text();
    console.error("Resend error:", resendResp.status, resendErr);
    return new Response(
      JSON.stringify({ success: false, error: `Email non envoyé : ${resendErr}` }),
      { headers: corsHeaders, status: 500 }
    );
  }

  return new Response(JSON.stringify({ success: true }), { headers: corsHeaders });
});
