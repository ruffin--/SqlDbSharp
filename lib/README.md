**Note that the below DOES NOT apply for the Windows Phone 8 version of SqlDbSharp, where code to the extra dll REMAINS INTEGRATED in the project.**  A little hacky, but very convenient.

I wanted to keep this project completely free of 3rd party libraries or even of 
external dlls as long as possible, but there's just enough headache with these
extensions, both with respect to code licensing and versioning with my other projects,
that I figured I'd split it off into its own project.

Apologies for the overhead.  The code to RufworkExtensions.dll can be found here:
https://github.com/ruffin--/RufworkExtensions

I'm going to place a Windows version of the dll (not Windows Phone; strict Windows) in
the lib folder as well.