// Create ContentKeyAuthorizationPolicy

using System.Security.Cryptography.X509Certificates;

string jwtAuthorizationPolicy = "JWT Authorization Policy";
  IContentKeyAuthorizationPolicy policy = await objCloudMediaContext.ContentKeyAuthorizationPolicies.CreateAsync(jwtAuthorizationPolicy);

  List<ContentKeyAuthorizationPolicyRestriction> restrictions = new List<ContentKeyAuthorizationPolicyRestriction>();

  //Class is used to represent ContentKeyAuthorizationPolicyRestriction.Requerements
  TokenRestrictionTemplate restrictionTemplate = new TokenRestrictionTemplate();

  //Specifies which token format will be used in token reques: TokenType.JWT or TokenType.SWT
  restrictionTemplate.TokenType = TokenType.JWT;

  //Instructing that token is signed with assymmetric key
  var templatex509Certificate2 = new X509Certificate2("cer.pfx", "certPassword");

  TokenRestrictionTemplate tokenRestrictionTemplate = new TokenRestrictionTemplate();
  tokenRestrictionTemplate.PrimaryVerificationKey = new X509CertTokenVerificationKey(templatex509Certificate2);

  //Specify that only tokens issued to specific aufience will be valid for content key delivery
  restrictionTemplate.Audience = new Uri("http://myaudience");

  //Instructing that only tokens issues by specific issuer will be valid for content key delivery
  restrictionTemplate.Issuer = new Uri("http://someIdentityserver");


  //Initilizing ContentKeyAuthorizationPolicyRestriction
  ContentKeyAuthorizationPolicyRestriction restriction = new ContentKeyAuthorizationPolicyRestriction
  {
      Name = "Authorization Policy with Token Restriction",
      KeyRestrictionType = (int)ContentKeyRestrictionType.TokenRestricted,
      Requirements = TokenRestrictionTemplateSerializer.Serialize(restrictionTemplate)};

  restrictions.Add(restriction);

  //Saving IContentKeyAuthorizationPolicyOption on server so it can be associated with IContentKeyAuthorizationPolicy
  IContentKeyAuthorizationPolicyOption policyOption = objCloudMediaContext.ContentKeyAuthorizationPolicyOptions.Create("myDynamicEncryptionPolicy", ContentKeyDeliveryType.BaselineHttp, restrictions, String.Empty);
  policy.Options.Add(policyOption);

  //Saving Policy
  policy.UpdateAsync();

  // Add IContentKeyAuthorizationPolicy to ContentKey
  objIContentKey.AuthorizationPolicyId = policy.Id;
  IContentKey IContentKeyUpdated = await objIContentKey.UpdateAsync();