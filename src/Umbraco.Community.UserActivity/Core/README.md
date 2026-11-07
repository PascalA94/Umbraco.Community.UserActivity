Line-agnostic code lives here: it compiles unchanged on both Umbraco lines (`UmbracoMajor` 17 and 18).
Anything that differs per Umbraco line goes in `Web/`, `Client/` or the build files, behind `#if UMBRACO_17` / `#if UMBRACO_18`.
