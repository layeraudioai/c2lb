echo off
Set /P clean=clean(type clean if desired):
Set /P tools=tools(type tools if dir2exe desired, needed for packing single exe or samplegen):
Set /P samples=samples(type samples if sample generation desired):
Set /P content=content(type content if content has changed within content dir like new font or samples):
Set /P pack=pack(type pack if single exe output final file desired):

build %clean% %tools% %samples% %content% %pack%
