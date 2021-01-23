# Makefile for building ModuleManager

KSPDIR  := ${HOME}/.local/share/Steam/SteamApps/common/Kerbal\ Space\ Program
MANAGED := KSP_Data/Managed/

INCLUDEFILES := $(wildcard *.cs) \
	$(wildcard Properties/*.cs)

RESGEN2 := resgen2
GMCS    := /usr/bin/gmcs
GIT     := /usr/bin/git
TAR     := /usr/bin/tar
ZIP     := /usr/bin/zip

all: build

info:
	@echo "== ModuleManager Build Information =="
	@echo "  gmcs:    ${GMCS}"
	@echo "  git:     ${GIT}"
	@echo "  tar:     ${TAR}"
	@echo "  zip:     ${ZIP}"
	@echo "  KSP Data: ${KSPDIR}"
	@echo "====================================="

build: info
	mkdir -p build
	${RESGEN2} -usesourcepath Properties/Resources.resx build/Resources.resources
	${GMCS} -t:library -lib:${KSPDIR}/${MANAGED} \
		-r:Assembly-CSharp,Assembly-CSharp-firstpass,UnityEngine \
		-out:build/ModuleManager.dll \
		-resource:build/Resources.resources,ModuleManager.Properties.Resources.resources \
		${INCLUDEFILES}

tar.gz: build
	${TAR} zcf ModuleManager-0.$(shell ${GIT} rev-list --count HEAD).g$(shell ${GIT} log -1 --format="%h").tar.gz ModuleManager.version build/ModuleManager.dll

zip: build
	${ZIP} -9 -r ModuleManager-0.$(shell ${GIT} rev-list --count HEAD).g$(shell ${GIT} log -1 --format="%h").zip ModuleManager.version build/ModuleManager.dll

clean:
	@echo "Cleaning up build and package directories..."
	rm -rf build/ package/

install: build
	mkdir -p ${KSPDIR}/GameData/
	cp build/ModuleManager.dll ${KSPDIR}/GameData/

uninstall: info
	rm -f ${KSPDIR}/GameData/ModuleManager.dll


.PHONY : all info build tar.gz zip clean install uninstall
