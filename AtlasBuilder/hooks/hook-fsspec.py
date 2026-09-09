# AtlasBuilder only opens local Unity asset files. The generic contrib hook collects every
# optional remote backend and can pull hundreds of megabytes of unrelated data-science packages.
hiddenimports = ["fsspec.implementations.local"]
