﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace MrHuo.OAuth.DingTalk
{
    /// <summary>
    /// 文档：
    /// https://ding-doc.dingtalk.com/document/app/logon-free-third-party-websites#topic-2021767
    /// 钉钉内部登录时：scope=snsapi_auth
    /// </summary>
    public class DingTalkOAuth : OAuthLoginBase<DingTalkUserInfoModel>
    {
        public DingTalkOAuth(OAuthConfig oauthConfig) : base(oauthConfig)
        {
        }

        protected override string AuthorizeUrl => "https://oapi.dingtalk.com/connect/oauth2/sns_authorize";

        protected override string AccessTokenUrl => throw new NotSupportedException();

        protected override string UserInfoUrl => "https://oapi.dingtalk.com/sns/getuserinfo_bycode";

        protected override Dictionary<string, string> BuildAuthorizeParams(string state)
        {
            return new Dictionary<string, string>
            {
                ["response_type"] = "code",
                ["appid"] = (oauthConfig.AppId ?? ""),
                ["redirect_uri"] = (oauthConfig.RedirectUri ?? ""),
                ["scope"] = (oauthConfig.Scope ?? ""),
                ["state"] = (state ?? "")
            };
        }

        /// <summary>
        /// 钉钉扫码登录没有 access_token，故直接返回 code 
        /// </summary>
        /// <param name="authorizeCallbackParams"></param>
        /// <returns></returns>
        public override Task<DefaultAccessTokenModel> GetAccessTokenAsync(Dictionary<string, string> authorizeCallbackParams)
        {
            return Task.FromResult(new DefaultAccessTokenModel()
            {
                AccessToken = authorizeCallbackParams["code"]
            });
        }

        protected override Dictionary<string, string> BuildGetUserInfoParams(DefaultAccessTokenModel accessTokenModel)
        {
            var timestamp = DingTalkSignTool.GetTimestamp();
            var sign = DingTalkSignTool.Sign(timestamp, oauthConfig.AppKey);
            return new Dictionary<string, string>
            {
                ["accessKey"] = oauthConfig.AppId,
                ["timestamp"] = timestamp,
                ["signature"] = sign
            };
        }

        public override async Task<DingTalkUserInfoModel> GetUserInfoAsync(DefaultAccessTokenModel accessTokenModel)
        {
            //https://ding-doc.dingtalk.com/document/app/obtain-the-user-information-based-on-the-sns-temporary-authorization#topic-1995619
            var queryString = ToQueryString(BuildGetUserInfoParams(accessTokenModel));
            var api = $"{UserInfoUrl}?{queryString}";
            var responseText = await HttpRequestApi.PostJsonBodyAsync(api, new Dictionary<string, string>
            {
                //这里的 accessTokenModel.AccessToken 实质是回调页面 code，钉钉应用内登录不支持 access token
                ["tmp_auth_code"] = accessTokenModel.AccessToken
            });
            var userInfoApiResponse = JsonSerializer.Deserialize<DingTalkGetUserInfoApiResponse>(responseText);
            if (userInfoApiResponse.HasError())
            {
                throw new Exception(userInfoApiResponse.ErrorMessage);
            }
            return userInfoApiResponse.UserInfo;
        }

        private static string ToQueryString(Dictionary<string, string> dict)
        {
            return string.Join("&", dict.Select(p => $"{DingTalkSignTool.UrlEncode(p.Key)}={DingTalkSignTool.UrlEncode(p.Value)}"));
        }
    }
}
