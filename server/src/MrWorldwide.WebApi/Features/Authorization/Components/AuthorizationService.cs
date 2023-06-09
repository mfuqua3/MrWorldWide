﻿using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using MrWorldwide.WebApi.Data.Entities;
using MrWorldwide.WebApi.Features.Authorization.Domain;
using MrWorldwide.WebApi.Features.Authorization.Extensions;

namespace MrWorldwide.WebApi.Features.Authorization.Components;

public class AuthorizationService
{
    private readonly IGoogleAuthenticationEngine _googleAuthenticationEngine;
    private readonly IJwtEngine _jwtEngine;
    private readonly UserManager<AppUser> _userManager;

    public AuthorizationService(
        IGoogleAuthenticationEngine googleAuthenticationEngine,
        IJwtEngine jwtEngine,
        UserManager<AppUser> userManager)
    {
        _googleAuthenticationEngine = googleAuthenticationEngine;
        _jwtEngine = jwtEngine;
        _userManager = userManager;
    }

    public async Task<TokenResponse> AuthorizeGoogleSsoAsync(TokenRequest request)
    {
        var claimsIdentity = await _googleAuthenticationEngine.AuthenticateGoogleSsoAsync(request.IdToken);
        var googleId = claimsIdentity.Claims.Single(x => x.Type == "google_id").Value;
        var user = await _userManager.FindByLoginAsync("Google", googleId);
        if (user == null)
        {
            var name = claimsIdentity.Claims.Single(x => x.Type == "name").Value;
            var email = claimsIdentity.Claims.Single(x => x.Type == "email").Value;
            user = new AppUser { Email = email, EmailConfirmed = true, UserName = email, Name = name };
            await _userManager.CreateAsync(user);
            await _userManager.AddLoginAsync(user,
                new ExternalLoginInfo(new ClaimsPrincipal(claimsIdentity), "Google", googleId, "Google SSO"));
            await _userManager.AddClaimsAsync(user, claimsIdentity.Claims.Append(new Claim("sub", user.Id)));
        }

        claimsIdentity.AddClaim(new Claim("sub", user.Id));
        var jwt = _jwtEngine.WriteToken(claimsIdentity.Claims);
        var refreshToken = await _userManager.GenerateRefreshTokenAsync(user);
        return new TokenResponse
        {
            AccessToken = jwt,
            RefreshToken = refreshToken
        };
    }

    public async Task<TokenResponse> RefreshAuthenticatedUserAsync(RefreshRequest refreshRequest)
    {
        var user = await _userManager.FindByIdAsync(refreshRequest.UserId);
        if (user == null)
        {
            throw new KeyNotFoundException("No user with that ID could be found");
        }

        var canRefresh = !string.IsNullOrEmpty(user.RefreshToken) &&
                         user.RefreshTokenExpiryTime > DateTime.UtcNow &&
                         string.Equals(refreshRequest.RefreshToken, user.RefreshToken, StringComparison.Ordinal);
        if (!canRefresh)
        {
            throw new InvalidOperationException("Invalid Refresh Token");
        }

        var claims = await _userManager.GetClaimsAsync(user);
        var refreshToken = await _userManager.GenerateRefreshTokenAsync(user);
        var jwt = _jwtEngine.WriteToken(claims);
        return new TokenResponse
        {
            RefreshToken = refreshToken,
            AccessToken = jwt
        };
    }

    public async Task SignOutUserAsync(SignoutRequest signOutRequest)
    {
        var user = await _userManager.FindByIdAsync(signOutRequest.UserId);
        if (user == null)
        {
            throw new KeyNotFoundException("No user with that ID could be found");
        }

        await _userManager.RevokeRefreshTokenAsync(user);
    }
}