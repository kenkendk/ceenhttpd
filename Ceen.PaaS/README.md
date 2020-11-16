+++ Ceen.PaaS : A platform for building websites +++

Building a fully feature website has numerous parts that need to work in tandem to get security, features and performance in just the right mix.

With the Ceen.PaaS module you get a number of these features pre-built. This includes:

* Users with roles, admin is built-in
* User account management, secure password storage
  * Create user
  * Verify email
  * Reset password
* Locally stored and resized images
* Markdown based rendering to html (and plaintext)
* Email transmission and retries using Sparkpost
* ToS and privacy documents from Markdown
* Login checks, including Admin checks
  * Fully featured XSRF protection
* Locale support for localized messages/pages
* Admin access to monitor queues, signups, emails and http requests

Where as Ceen is generally written to be flexible and fit many different approaches, the PaaS module has some choices that are not configureable. It is however possible simply avoid some of the features if they are not relevant, while still maintaining the remaining functionality.

To configure the PaaS module, load it from you application and then use a configuration similar to this:

```
httpport 12345
httpaddress loopback

# Setup loggers
logger Ceen.Httpd.Logging.StdOutErrors
logger Ceen.Extras.LogModule

# Run the modules
module Ceen.Extras.MemCache

module Ceen.Extras.QueueModule "emailqueue"
set ratelimit 4/s
set ConnectionString "queues.sqlite"

# The main database
module Ceen.PaaS.DatabaseInstance
set ConnectionString "Ceen.PaaS.sqlite"

# The short and long-term tokens
module Ceen.Security.Login.DatabaseStorageModule true true false

# Custom logout feature
handler "/api/v1/logout" Ceen.Security.Login.LogoutHandler
set ResultStatusCode 200
set ResultStatusMessage OK
set RedirectUrl ""

# Require XSRF on all API calls
handler "/api/*" Ceen.Security.Login.XSRFTokenRequiredHandler
handler "/api/v1/login" Ceen.Security.Login.LoginHandler

# Attach user information to later handlers
handler "/api/*" Ceen.PaaS.Services.LoginChecker

# Admin stuff needs admin access
handler "/api/v1/admin/*" Ceen.PaaS.Services.AdminRequiredHandler

# Generate XSRF tokens for non-api pages
handler "" Ceen.Security.Login.XSRFTokenGeneratorHandler

# Wire up the API
route Ceen.PaaS

# Anything not caught by the API gives 404
handler "/api/*" Ceen.Httpd.Handler.StaticHandler
set StatusCode 404

handler "/tos" Ceen.PaaS.Services.TermsOfService
handler "/privacy" Ceen.PaaS.Services.PrivacyPolicy
```

Further, to avoid leaking various sensitive information, the PaaS module expects a file named `secrets.txt` to be placed in the folder where Ceen is running from. Inside this file can be any number of `key=value` items, that are readable from the `Ceen.PaaS.SecretsManager` class. 

You can add any items specific to your application, but you need to set up the Sparkpost API key and the default admin user to get started. Example `secrets.txt` file:
```
SPARKPOST_KEY = 0123456789

DEFAULT_ADMIN_EMAIL=example@example.com
DEFAULT_ADMIN_PASSWORD=examplepassword
```