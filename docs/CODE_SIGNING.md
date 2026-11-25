# Windows code signing

A signed installer requires an Authenticode code-signing certificate issued to the publisher. The private key cannot be included in this source package.

Recommended release order:

1. Build the self-contained executable and installer with `tools/build-release.ps1`.
2. Sign both files with `tools/sign-release.ps1 -CertificateThumbprint YOUR_THUMBPRINT`.
3. Verify the signatures in Windows file Properties → Digital Signatures.
4. Test installation on a clean Windows 10 and Windows 11 computer.
5. Generate `update.json` only after signing the final installer.

For broader SmartScreen reputation, use a certificate and release process appropriate to your organisation. Keep the certificate private key in secure certificate storage or a hardware-backed signing service.
