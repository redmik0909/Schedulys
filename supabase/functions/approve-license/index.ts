import { createClient } from "https://esm.sh/@supabase/supabase-js@2";

const supabase = createClient(
  Deno.env.get("SUPABASE_URL")!,
  Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!
);

const RESEND_API_KEY = Deno.env.get("RESEND_API_KEY")!;
const FROM_EMAIL     = Deno.env.get("FROM_EMAIL") ?? "Schedulys <noreply@revolvittech.com>";

function generateLicenseKey(): string {
  const chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // sans 0/O/1/I pour éviter la confusion
  const seg = (n: number) =>
    Array.from({ length: n }, () => chars[Math.floor(Math.random() * chars.length)]).join("");
  return `SCHEDULYS-${seg(4)}-${seg(4)}-${seg(4)}-${seg(4)}`;
}

function htmlSuccess(school: string, email: string, key: string, expires: string, trial: boolean): string {
  return `<!DOCTYPE html><html><body style="font-family:sans-serif;max-width:520px;margin:60px auto;text-align:center">
    <h2 style="color:#16A34A">${trial ? "🔬 Licence d'essai activée !" : "✅ Licence approuvée !"}</h2>
    ${trial ? `<p style="background:#FEF3C7;padding:10px;border-radius:8px;color:#92400E">Licence d'essai — expire dans 30 jours</p>` : ""}
    <p>Un courriel avec la clé a été envoyé à <strong>${email}</strong>.</p>
    <p>École : <strong>${school}</strong></p>
    <p>Clé : <code style="background:#f0f0f0;padding:4px 10px;border-radius:4px;font-size:16px">${key}</code></p>
    <p>Expire le : ${expires}</p>
  </body></html>`;
}

function htmlEmail(school: string, contact: string, key: string, expires: string): string {
  return `<!DOCTYPE html><html><body style="font-family:sans-serif;max-width:560px;margin:0 auto">
    <h2 style="color:#4F46E5">Bienvenue sur Schedulys !</h2>
    <p>Bonjour ${contact},</p>
    <p>Votre licence pour <strong>${school}</strong> a été activée.</p>
    <div style="background:#F1F5F9;border-radius:8px;padding:20px;margin:24px 0;text-align:center">
      <p style="margin:0 0 8px;font-size:12px;color:#64748B;letter-spacing:1px">CLÉ DE LICENCE</p>
      <p style="margin:0;font-family:Consolas,monospace;font-size:20px;color:#0F172A;letter-spacing:2px">${key}</p>
    </div>
    <p><strong>Expire le :</strong> ${expires}</p>
    <p style="margin-top:24px">Pour activer :</p>
    <ol>
      <li>Ouvrez Schedulys</li>
      <li>Cliquez sur <strong>Licence</strong> dans le menu gauche</li>
      <li>Cliquez sur <strong>Changer la clé</strong></li>
      <li>Entrez la clé ci-dessus</li>
    </ol>
    <hr style="margin:32px 0;border:none;border-top:1px solid #E2E8F0"/>
    <p style="font-size:11px;color:#94A3B8">Revolvit Technologies · support@revolvittech.com</p>
  </body></html>`;
}

Deno.serve(async (req) => {
  const url       = new URL(req.url);
  const requestId = url.searchParams.get("request_id");
  const token     = url.searchParams.get("token");
  const isTrial   = url.searchParams.get("trial") === "true";

  const htmlHeaders = { "Content-Type": "text/html; charset=utf-8" };

  if (!requestId || !token) {
    return new Response("<h2>Paramètres manquants.</h2>", { status: 400, headers: htmlHeaders });
  }

  // Récupère la demande et vérifie le token
  const { data: request, error: fetchError } = await supabase
    .from("license_requests")
    .select("*")
    .eq("id", requestId)
    .eq("approval_token", token)
    .single();

  if (fetchError || !request) {
    return new Response("<h2>Demande introuvable ou token invalide.</h2>", {
      status: 404, headers: htmlHeaders,
    });
  }

  if (request.status !== "pending") {
    return new Response(
      `<html><body style="font-family:sans-serif;max-width:480px;margin:60px auto;text-align:center">
        <h2>Demande déjà traitée</h2>
        <p>Statut : <strong>${request.status}</strong></p>
        ${request.license_key ? `<p>Clé : <code>${request.license_key}</code></p>` : ""}
      </body></html>`,
      { headers: htmlHeaders }
    );
  }

  // Génère la clé et la date d'expiration
  const licenseKey = generateLicenseKey();
  const expiresAt  = new Date();
  if (isTrial) {
    expiresAt.setDate(expiresAt.getDate() + 30);
  } else {
    expiresAt.setFullYear(expiresAt.getFullYear() + 1);
  }
  const expiresStr = expiresAt.toISOString().split("T")[0];
  const expiresFr  = expiresAt.toLocaleDateString("fr-CA", { year: "numeric", month: "long", day: "numeric" });

  // Insère dans licenses
  const { error: insertError } = await supabase.from("licenses").insert({
    license_key:     licenseKey,
    school_name:     request.school_name,
    email:           request.email,
    expires_at:      expiresStr,
    max_activations: 3,
    is_trial:        isTrial,
  });

  if (insertError) {
    return new Response(`<h2>Erreur lors de la création : ${insertError.message}</h2>`, {
      status: 500, headers: htmlHeaders,
    });
  }

  // Met à jour le statut de la demande
  await supabase
    .from("license_requests")
    .update({ status: "approved", license_key: licenseKey, approved_at: new Date().toISOString() })
    .eq("id", requestId);

  // Envoie le courriel via Resend
  await fetch("https://api.resend.com/emails", {
    method: "POST",
    headers: {
      "Authorization": `Bearer ${RESEND_API_KEY}`,
      "Content-Type":  "application/json",
    },
    body: JSON.stringify({
      from:    FROM_EMAIL,
      to:      request.email,
      subject: `Votre clé de licence Schedulys — ${request.school_name}`,
      html:    htmlEmail(request.school_name, request.contact_name, licenseKey, expiresFr),
    }),
  });

  return new Response(
    htmlSuccess(request.school_name, request.email, licenseKey, expiresFr, isTrial),
    { headers: htmlHeaders }
  );
});
