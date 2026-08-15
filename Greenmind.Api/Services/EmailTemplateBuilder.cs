using System.Text;
using System.Text.Encodings.Web;

namespace Greenmind.Api.Services;

public static class EmailTemplateBuilder
{
    public static string BuildAdminInvitationEmail(
        string fullName,
        string email,
        string actionUrl,
        string frontendBaseUrl)
    {
        return BuildEmailLayout(
            previewText: "You have been invited to join the GreenMind administration team.",
            categoryBadge: "Admin Portal | بوابة الإدارة",
            iconType: IconType.Invitation,
            title: "Administrator Invitation",
            arabicTitle: "دعوة للانضمام إلى فريق إدارة جرين مايند",
            greeting: $"Hello {Encode(fullName)},",
            bodyHtml: $"""
                <p style="margin:0 0 16px 0; color:#3e4f45; font-size:15px; line-height:1.7;">
                    You have been invited by a platform super administrator to join <strong>GreenMind</strong> as an <strong>Administrator</strong>.
                </p>
                <p style="margin:0 0 16px 0; color:#3e4f45; font-size:15px; line-height:1.7;">
                    As an administrator, you will have access to manage farm accounts, review export plans, inspect AI usage diagnostics, and manage system operations. Click the button below to set your password and activate your account.
                </p>
                """,
            actionLabel: "Activate Admin Account / تفعيل حساب المسؤول",
            actionUrl: actionUrl,
            details:
            [
                ("Invited Email / البريد المدعو", Encode(email)),
                ("Assigned Role / الدور", "<span style=\"display:inline-block; padding:3px 10px; background:#e8f7ed; color:#087a42; border-radius:20px; font-weight:700; font-size:12px;\">Platform Admin</span>"),
                ("Link Validity / صلاحية الرابط", "<span style=\"color:#b26414; font-weight:700;\">7 Days / ٧ أيام</span>")
            ],
            noticeBox: """
                <div style="background:#fffbeb; border:1px solid #fde68a; border-radius:10px; padding:14px 16px; margin:20px 0 0 0;">
                    <div style="color:#92400e; font-size:13px; font-weight:700; margin-bottom:4px;">🔒 Security Notice / تنبيه أمان</div>
                    <div style="color:#78350f; font-size:12px; line-height:1.6;">
                        This invitation link is personal and will expire in 7 days. If you were not expecting this invitation, please contact your platform administrator immediately.
                    </div>
                </div>
                """,
            frontendBaseUrl: frontendBaseUrl);
    }

    public static string BuildAccountActivationEmail(
        string fullName,
        string email,
        string activationUrl,
        string frontendBaseUrl)
    {
        return BuildEmailLayout(
            previewText: "Activate your GreenMind account to get started.",
            categoryBadge: "Account Activation | تفعيل الحساب",
            iconType: IconType.Approved,
            title: "Activate Your GreenMind Account",
            arabicTitle: "تفعيل حسابك في منصة جرين مايند",
            greeting: $"Hello {Encode(fullName)},",
            bodyHtml: $"""
                <p style="margin:0 0 16px 0; color:#3e4f45; font-size:15px; line-height:1.7;">
                    Thank you for joining <strong>GreenMind</strong>.
                </p>
                <p style="margin:0 0 16px 0; color:#3e4f45; font-size:15px; line-height:1.7;">
                    To complete your registration and activate your smart agricultural management dashboard, please click the button below to confirm your email address.
                </p>
                """,
            actionLabel: "Activate Account / تفعيل الحساب",
            actionUrl: activationUrl,
            details:
            [
                ("Account Email / البريد الإلكتروني", Encode(email)),
                ("Link Expiry / مدة الصلاحية", "<span style=\"color:#b26414; font-weight:700;\">24 Hours / ٢٤ ساعة</span>")
            ],
            noticeBox: """
                <div style="background:#fffbeb; border:1px solid #fde68a; border-radius:10px; padding:14px 16px; margin:20px 0 0 0;">
                    <div style="color:#92400e; font-size:13px; font-weight:700; margin-bottom:4px;">🔒 Security Notice / تنبيه أمان</div>
                    <div style="color:#78350f; font-size:12px; line-height:1.6;">
                        This activation link is intended solely for your account. If you did not sign up for GreenMind, please safely ignore this email.
                    </div>
                </div>
                """,
            frontendBaseUrl: frontendBaseUrl);
    }

    public static string BuildRegistrationReceivedEmail(
        string fullName,
        string farmName,
        string loginUrl,
        string frontendBaseUrl)
    {
        return BuildEmailLayout(
            previewText: "Your GreenMind farm account registration has been received and is under review.",
            categoryBadge: "Account Registration | تسجيل حساب",
            iconType: IconType.Review,
            title: "Registration Under Review",
            arabicTitle: "تم استلام طلب تسجيل المزرعة وهو قيد المراجعة",
            greeting: $"Hello {Encode(fullName)},",
            bodyHtml: $"""
                <p style="margin:0 0 16px 0; color:#3e4f45; font-size:15px; line-height:1.7;">
                    Thank you for registering <strong>{Encode(farmName)}</strong> with <strong>GreenMind</strong>.
                </p>
                <p style="margin:0 0 16px 0; color:#3e4f45; font-size:15px; line-height:1.7;">
                    Our administrative team is currently reviewing your application and farm details to ensure compliance and prepare your smart farming dashboard.
                </p>
                <!-- Visual Pipeline Tracker -->
                <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="margin:20px 0; background:#f8faf8; border:1px solid #e7efe9; border-radius:12px; padding:16px;">
                    <tr>
                        <td align="center" style="width:33%; padding:8px 4px;">
                            <div style="width:28px; height:28px; line-height:28px; background:#087a42; color:#ffffff; border-radius:50%; font-size:14px; font-weight:bold; margin:0 auto 6px auto;">✓</div>
                            <div style="font-size:12px; font-weight:700; color:#0c261b;">Submitted</div>
                            <div style="font-size:10px; color:#64766d;">تم الإرسال</div>
                        </td>
                        <td align="center" style="width:33%; padding:8px 4px; border-left:1px solid #e1e8e3; border-right:1px solid #e1e8e3;">
                            <div style="width:28px; height:28px; line-height:28px; background:#d7f45e; color:#0c261b; border-radius:50%; font-size:14px; font-weight:bold; margin:0 auto 6px auto;">⏳</div>
                            <div style="font-size:12px; font-weight:700; color:#087a42;">Under Review</div>
                            <div style="font-size:10px; color:#087a42;">قيد المراجعة</div>
                        </td>
                        <td align="center" style="width:33%; padding:8px 4px;">
                            <div style="width:28px; height:28px; line-height:28px; background:#e2e8e0; color:#8a9990; border-radius:50%; font-size:14px; font-weight:bold; margin:0 auto 6px auto;">3</div>
                            <div style="font-size:12px; font-weight:700; color:#8a9990;">Active</div>
                            <div style="font-size:10px; color:#8a9990;">التفعيل والبدء</div>
                        </td>
                    </tr>
                </table>
                """,
            actionLabel: "Visit GreenMind Platform / زيارة المنصة",
            actionUrl: loginUrl,
            details:
            [
                ("Farm Name / اسم المزرعة", Encode(farmName)),
                ("Owner Name / اسم المالك", Encode(fullName)),
                ("Current Status / الحالة", "<span style=\"display:inline-block; padding:3px 10px; background:#fef3c7; color:#b45309; border-radius:20px; font-weight:700; font-size:12px;\">⏳ Pending Approval / بانتظار الموافقة</span>"),
                ("Next Step / الخطوة القادمة", "You will receive an email once approved / سيتم إشعاركم فور الاعتماد")
            ],
            noticeBox: null,
            frontendBaseUrl: frontendBaseUrl);
    }

    public static string BuildNewRegistrationAlertEmail(
        string adminFullName,
        string ownerFullName,
        string ownerEmail,
        string farmName,
        int farmId,
        string reviewUrl,
        string frontendBaseUrl)
    {
        return BuildEmailLayout(
            previewText: $"New farm registration '{farmName}' submitted and awaiting admin approval.",
            categoryBadge: "Admin Alert | تنبيه إداري",
            iconType: IconType.Alert,
            title: "New Farm Registration Pending Approval",
            arabicTitle: "طلب تسجيل مزرعة جديدة بانتظار الاعتماد",
            greeting: "Hello GreenMind Administrator,",
            bodyHtml: $"""
                <p style="margin:0 0 16px 0; color:#3e4f45; font-size:15px; line-height:1.7;">
                    A new agricultural enterprise registration has been submitted by <strong>{Encode(ownerFullName)}</strong>.
                </p>
                <p style="margin:0 0 16px 0; color:#3e4f45; font-size:15px; line-height:1.7;">
                    Please review the farm account details in the administration dashboard to approve or manage this account.
                </p>
                """,
            actionLabel: "Review Farm Account / مراجعة حساب المزرعة",
            actionUrl: reviewUrl,
            details:
            [
                ("Farm ID / رقم المزرعة", $"#{farmId}"),
                ("Farm Name / اسم المزرعة", Encode(farmName)),
                ("Owner Name / المالك", Encode(ownerFullName)),
                ("Owner Email / البريد الإلكتروني", Encode(ownerEmail)),
                ("Status / الحالة", "<span style=\"display:inline-block; padding:3px 10px; background:#fef3c7; color:#b45309; border-radius:20px; font-weight:700; font-size:12px;\">Requires Approval</span>")
            ],
            noticeBox: null,
            frontendBaseUrl: frontendBaseUrl);
    }

    public static string BuildFarmApprovedEmail(
        string fullName,
        string farmName,
        string dashboardUrl,
        string frontendBaseUrl)
    {
        return BuildEmailLayout(
            previewText: $"Congratulations! Your GreenMind account for {farmName} is approved and active.",
            categoryBadge: "Account Approved | تم اعتماد الحساب",
            iconType: IconType.Approved,
            title: "Welcome to GreenMind!",
            arabicTitle: "تهانينا! تم اعتماد حساب المزرعة وتفعيله بنجاح",
            greeting: $"Welcome, {Encode(fullName)}!",
            bodyHtml: $"""
                <p style="margin:0 0 16px 0; color:#3e4f45; font-size:15px; line-height:1.7;">
                    We are excited to inform you that your farm <strong>{Encode(farmName)}</strong> has been verified and approved by the GreenMind administration team.
                </p>
                <p style="margin:0 0 16px 0; color:#3e4f45; font-size:15px; line-height:1.7;">
                    Your account is now fully active. You can now access your comprehensive smart agricultural suite:
                </p>
                <!-- Feature Highlights -->
                <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="margin:16px 0; background:#f8faf8; border:1px solid #e7efe9; border-radius:12px; padding:14px;">
                    <tr>
                        <td style="padding:8px 0; border-bottom:1px solid #edf2ee;">
                            <span style="font-size:16px; margin-right:8px;">🌱</span>
                            <strong style="color:#0c261b; font-size:13px;">AI Export Planning:</strong>
                            <span style="color:#64766d; font-size:13px;"> Generate compliant export schedules tailored to international standards.</span>
                        </td>
                    </tr>
                    <tr>
                        <td style="padding:8px 0; border-bottom:1px solid #edf2ee;">
                            <span style="font-size:16px; margin-right:8px;">⛅</span>
                            <strong style="color:#0c261b; font-size:13px;">Smart Weather & Irrigation:</strong>
                            <span style="color:#64766d; font-size:13px;"> Hyper-local weather forecasting and automated irrigation planning.</span>
                        </td>
                    </tr>
                    <tr>
                        <td style="padding:8px 0; border-bottom:1px solid #edf2ee;">
                            <span style="font-size:16px; margin-right:8px;">🔍</span>
                            <strong style="color:#0c261b; font-size:13px;">Crop Health & Diagnostics:</strong>
                            <span style="color:#64766d; font-size:13px;"> Instant AI disease detection from crop photography.</span>
                        </td>
                    </tr>
                    <tr>
                        <td style="padding:8px 0;">
                            <span style="font-size:16px; margin-right:8px;">📊</span>
                            <strong style="color:#0c261b; font-size:13px;">Farm Operations & Finances:</strong>
                            <span style="color:#64766d; font-size:13px;"> Manage daily activities, track expenses, and monitor harvests.</span>
                        </td>
                    </tr>
                </table>
                """,
            actionLabel: "Launch Your Dashboard / الدخول إلى لوحة التحكم",
            actionUrl: dashboardUrl,
            details:
            [
                ("Farm Name / اسم المزرعة", Encode(farmName)),
                ("Account Status / حالة الحساب", "<span style=\"display:inline-block; padding:3px 10px; background:#e8f7ed; color:#087a42; border-radius:20px; font-weight:700; font-size:12px;\">✓ Active & Verified / مفعل ومعتمد</span>")
            ],
            noticeBox: null,
            frontendBaseUrl: frontendBaseUrl);
    }

    public static string BuildPasswordResetEmail(
        string fullName,
        string email,
        string resetUrl,
        string frontendBaseUrl)
    {
        return BuildEmailLayout(
            previewText: "Reset your GreenMind account password.",
            categoryBadge: "Security Alert | أمان الحساب",
            iconType: IconType.PasswordReset,
            title: "Password Reset Request",
            arabicTitle: "طلب إعادة تعيين كلمة المرور",
            greeting: $"Hello {Encode(fullName)},",
            bodyHtml: $"""
                <p style="margin:0 0 16px 0; color:#3e4f45; font-size:15px; line-height:1.7;">
                    We received a request to reset the password for your GreenMind account (<strong>{Encode(email)}</strong>).
                </p>
                <p style="margin:0 0 16px 0; color:#3e4f45; font-size:15px; line-height:1.7;">
                    To choose a new secure password, click the button below. This link is single-use and will expire shortly.
                </p>
                """,
            actionLabel: "Reset Password / إعادة تعيين كلمة المرور",
            actionUrl: resetUrl,
            details:
            [
                ("Account Email / البريد الإلكتروني", Encode(email)),
                ("Link Expiry / مدة الصلاحية", "<span style=\"color:#b26414; font-weight:700;\">1 Hour / ساعة واحدة</span>")
            ],
            noticeBox: """
                <div style="background:#fffbeb; border:1px solid #fde68a; border-radius:10px; padding:14px 16px; margin:20px 0 0 0;">
                    <div style="color:#92400e; font-size:13px; font-weight:700; margin-bottom:4px;">🔒 Security Notice / تنبيه أمان</div>
                    <div style="color:#78350f; font-size:12px; line-height:1.6;">
                        If you didn't request a password reset, you can safely ignore this email. Your password will remain unchanged and your account stays secure.
                    </div>
                </div>
                """,
            frontendBaseUrl: frontendBaseUrl);
    }

    private static string BuildEmailLayout(
        string previewText,
        string categoryBadge,
        IconType iconType,
        string title,
        string? arabicTitle,
        string greeting,
        string bodyHtml,
        string actionLabel,
        string actionUrl,
        IReadOnlyList<(string Label, string Value)> details,
        string? noticeBox,
        string frontendBaseUrl)
    {
        var iconHtml = GetIconSvg(iconType);

        var detailRows = string.Join("", details.Select(item => $"""
            <tr>
                <td style="padding:12px 14px; color:#64766d; font-size:13px; font-weight:600; border-bottom:1px solid #edf2ee; width:45%; vertical-align:middle;">
                    {item.Label}
                </td>
                <td style="padding:12px 14px; color:#0c261b; font-size:13px; font-weight:700; text-align:right; border-bottom:1px solid #edf2ee; vertical-align:middle;">
                    {item.Value}
                </td>
            </tr>
            """));

        var detailsCard = details.Count > 0 ? $"""
            <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="margin:22px 0 0 0; background:#f8faf8; border:1px solid #e7efe9; border-radius:12px; overflow:hidden; border-collapse:separate;">
                {detailRows}
            </table>
            """ : string.Empty;

        var arabicTitleHtml = !string.IsNullOrWhiteSpace(arabicTitle)
            ? $"<div style=\"color:#55695e; font-size:14px; font-weight:600; margin-top:4px;\" dir=\"rtl\">{Encode(arabicTitle)}</div>"
            : string.Empty;

        var safeNoticeBox = noticeBox ?? string.Empty;

        return $$"""
            <!doctype html>
            <html lang="en" xmlns="http://www.w3.org/1999/xhtml">
            <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1.0">
                <meta http-equiv="X-UA-Compatible" content="IE=edge">
                <title>{{Encode(title)}} - GreenMind</title>
                <!--[if mso]>
                <style type="text/css">
                    body, table, td, a { font-family: Arial, Helvetica, sans-serif !important; }
                </style>
                <![endif]-->
            </head>
            <body style="margin:0; padding:0; background-color:#f2f6f3; font-family:'Segoe UI', -apple-system, BlinkMacSystemFont, Roboto, 'Helvetica Neue', Arial, sans-serif; -webkit-font-smoothing:antialiased; color:#0c261b;">
                <!-- Preheader snippet -->
                <div style="display:none; font-size:1px; color:#f2f6f3; line-height:1px; max-height:0px; max-width:0px; opacity:0; overflow:hidden;">
                    {{Encode(previewText)}}
                </div>

                <!-- Main Wrapper -->
                <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background-color:#f2f6f3; padding:36px 12px;">
                    <tr>
                        <td align="center">
                            <!-- Container Card -->
                            <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="max-width:600px; width:100%; background-color:#ffffff; border:1px solid #dde6df; border-radius:16px; overflow:hidden; box-shadow:0 8px 30px rgba(12, 38, 27, 0.06);">
                                
                                <!-- Brand Header -->
                                <tr>
                                    <td style="background:#0c261b; background:linear-gradient(135deg, #0c261b 0%, #0d3824 100%); padding:24px 28px; border-bottom:3px solid #d7f45e;">
                                        <table role="presentation" width="100%" cellspacing="0" cellpadding="0">
                                            <tr>
                                                <td style="vertical-align:middle;">
                                                    <table role="presentation" cellspacing="0" cellpadding="0">
                                                        <tr>
                                                            <td style="vertical-align:middle; padding-right:12px;">
                                                                <div style="width:38px; height:38px; background:#ffffff; border-radius:10px; text-align:center; line-height:38px; overflow:hidden; display:inline-block;">
                                                                    <img src="{Encode(frontendBaseUrl)}/logo.png" width="38" height="38" alt="GreenMind" style="display:block; width:38px; height:38px; object-fit:contain; border:0;" />
                                                                </div>
                                                            </td>
                                                            <td style="vertical-align:middle;">
                                                                <div style="color:#ffffff; font-size:18px; font-weight:800; letter-spacing:0.02em; line-height:1.2;">
                                                                    GreenMind
                                                                </div>
                                                                <div style="color:#d7f45e; font-size:11px; font-weight:700; letter-spacing:0.04em;">
                                                                    نظام العقل الأخضر
                                                                </div>
                                                            </td>
                                                        </tr>
                                                    </table>
                                                </td>
                                                <td align="right" style="vertical-align:middle;">
                                                    <span style="display:inline-block; padding:5px 12px; background:rgba(215, 244, 94, 0.15); border:1px solid rgba(215, 244, 94, 0.35); color:#d7f45e; border-radius:20px; font-size:11px; font-weight:700; letter-spacing:0.03em;">
                                                        {{Encode(categoryBadge)}}
                                                    </span>
                                                </td>
                                            </tr>
                                        </table>
                                    </td>
                                </tr>

                                <!-- Content Section -->
                                <tr>
                                    <td style="padding:32px 30px 24px 30px;">
                                        
                                        <!-- Hero Icon & Title -->
                                        <div style="margin-bottom:20px;">
                                            {{iconHtml}}
                                            <h1 style="margin:12px 0 0 0; color:#0c261b; font-size:22px; font-weight:800; line-height:1.3;">
                                                {{Encode(title)}}
                                            </h1>
                                            {{arabicTitleHtml}}
                                        </div>

                                        <!-- Greeting -->
                                        <div style="font-size:16px; font-weight:700; color:#087a42; margin-bottom:14px;">
                                            {{greeting}}
                                        </div>

                                        <!-- Main Body -->
                                        <div style="font-size:15px; color:#3e4f45; line-height:1.7;">
                                            {{bodyHtml}}
                                        </div>

                                        <!-- CTA Button -->
                                        <div style="margin:28px 0 20px 0; text-align:center;">
                                            <!--[if mso]>
                                            <v:roundrect xmlns:v="urn:schemas-microsoft-com:vml" xmlns:w="urn:schemas-microsoft-com:office:word" href="{{Encode(actionUrl)}}" style="height:48px;v-text-anchor:middle;width:280px;" arcsize="25%" strokecolor="#055d31" fillcolor="#087a42">
                                                <w:anchorlock/>
                                                <center style="color:#ffffff;font-family:Arial,sans-serif;font-size:15px;font-weight:bold;">{{Encode(actionLabel)}}</center>
                                            </v:roundrect>
                                            <![endif]-->
                                            <!--[if !mso]><!-->
                                            <a href="{{Encode(actionUrl)}}" target="_blank" rel="noopener noreferrer" style="display:inline-block; background-color:#087a42; background:linear-gradient(135deg, #087a42 0%, #055d31 100%); color:#ffffff; font-size:15px; font-weight:800; text-decoration:none; padding:14px 28px; border-radius:12px; box-shadow:0 4px 14px rgba(8, 122, 66, 0.25); text-align:center; mso-padding-alt:0;">
                                                {{Encode(actionLabel)}}
                                            </a>
                                            <!--<![endif]-->
                                        </div>

                                        <!-- Details Card -->
                                        {{detailsCard}}

                                        <!-- Notice Box -->
                                        {{safeNoticeBox}}

                                        <!-- Direct Link Fallback -->
                                        <div style="margin-top:24px; padding-top:18px; border-top:1px solid #edf2ee;">
                                            <div style="color:#8a9990; font-size:11px; margin-bottom:6px;">
                                                If the button above does not work, copy and paste this link into your browser / إذا لم يعمل الزر أعلاه، انسخ الرابط التالي:
                                            </div>
                                            <div style="background:#f4f7f4; border:1px solid #e1e8e3; border-radius:8px; padding:8px 12px; font-family:Consolas, Monaco, monospace; font-size:11px; color:#087a42; word-break:break-all;">
                                                <a href="{{Encode(actionUrl)}}" style="color:#087a42; text-decoration:none;">{{Encode(actionUrl)}}</a>
                                            </div>
                                        </div>
                                    </td>
                                </tr>

                                <!-- Footer -->
                                <tr>
                                    <td style="background-color:#f8faf8; border-top:1px solid #e7efe9; padding:24px 30px; text-align:center;">
                                        <table role="presentation" width="100%" cellspacing="0" cellpadding="0">
                                            <tr>
                                                <td align="center">
                                                    <div style="font-size:13px; font-weight:700; color:#0c261b; margin-bottom:4px;">
                                                        GreenMind | نظام العقل الأخضر
                                                    </div>
                                                    <div style="font-size:11px; color:#64766d; line-height:1.5; max-width:480px; margin:0 auto 12px auto;">
                                                        Smart Agricultural Intelligence & Global Export Management Platform
                                                        <br />
                                                        المنصة الذكية لإدارة وتخطيط العمليات الزراعية والتصدير الدولي
                                                        <br />
                                                        <a href="mailto:uncle.khedr.greenmind@gmail.com" style="color:#087a42; text-decoration:none; font-weight:600;">uncle.khedr.greenmind@gmail.com</a>
                                                    </div>
                                                    <div style="font-size:11px; color:#8a9990; line-height:1.5;">
                                                        This is an automated system message. Please do not reply directly to this email.
                                                        <br />
                                                        &copy; 2026 GreenMind. All rights reserved.
                                                    </div>
                                                </td>
                                            </tr>
                                        </table>
                                    </td>
                                </tr>

                            </table>
                        </td>
                    </tr>
                </table>
            </body>
            </html>
            """;
    }

    private enum IconType
    {
        Invitation,
        Review,
        Alert,
        Approved,
        PasswordReset
    }

    private static string GetIconSvg(IconType type)
    {
        var (bgColor, strokeColor, svgContent) = type switch
        {
            IconType.Invitation => (
                "#e8f7ed",
                "#087a42",
                """<path d="M4 4h16c1.1 0 2 .9 2 2v12c0 1.1-.9 2-2 2H4c-1.1 0-2-.9-2-2V6c0-1.1.9-2 2-2z" stroke="#087a42" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/><path d="M22 6l-10 7L2 6" stroke="#087a42" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>"""
            ),
            IconType.Review => (
                "#fef3c7",
                "#b45309",
                """<circle cx="12" cy="12" r="10" stroke="#b45309" stroke-width="2"/><polyline points="12 6 12 12 16 14" stroke="#b45309" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>"""
            ),
            IconType.Alert => (
                "#fef3c7",
                "#b45309",
                """<path d="M18 8A6 6 0 0 0 6 8c0 7-3 9-3 9h18s-3-2-3-9" stroke="#b45309" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/><path d="M13.73 21a2 2 0 0 1-3.46 0" stroke="#b45309" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>"""
            ),
            IconType.Approved => (
                "#e8f7ed",
                "#087a42",
                """<path d="M22 11.08V12a10 10 0 1 1-5.93-9.14" stroke="#087a42" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/><polyline points="22 4 12 14.01 9 11.01" stroke="#087a42" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>"""
            ),
            IconType.PasswordReset => (
                "#fee2e2",
                "#be3a34",
                """<rect x="3" y="11" width="18" height="11" rx="2" ry="2" stroke="#be3a34" stroke-width="2"/><path d="M7 11V7a5 5 0 0 1 10 0v4" stroke="#be3a34" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>"""
            ),
            _ => (
                "#e8f7ed",
                "#087a42",
                """<circle cx="12" cy="12" r="10" stroke="#087a42" stroke-width="2"/>"""
            )
        };

        return $"""
            <div style="display:inline-block; width:44px; height:44px; background-color:{bgColor}; border-radius:12px; text-align:center; line-height:44px;">
                <svg width="24" height="24" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg" style="vertical-align:middle; display:inline-block;">
                    {svgContent}
                </svg>
            </div>
            """;
    }

    public static string StripHtmlForPlainText(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;

        var text = System.Text.RegularExpressions.Regex.Replace(html, @"<(script|style)[^>]*?>.*?</\1>", "", System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        text = System.Text.RegularExpressions.Regex.Replace(text, @"<br\s*/?>", "\n", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        text = System.Text.RegularExpressions.Regex.Replace(text, @"</p>", "\n\n", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        text = System.Text.RegularExpressions.Regex.Replace(text, @"</tr>", "\n", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        text = System.Text.RegularExpressions.Regex.Replace(text, @"<[^>]+>", " ");
        text = System.Net.WebUtility.HtmlDecode(text);
        text = System.Text.RegularExpressions.Regex.Replace(text, @"[ \t]+", " ");
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\n{3,}", "\n\n");
        return text.Trim();
    }

    private static string Encode(string value) => HtmlEncoder.Default.Encode(value ?? string.Empty);
}
