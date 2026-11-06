cmake_minimum_required(VERSION 3.15)
project(acl_native LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

set(ACL_ROOT "${CMAKE_CURRENT_SOURCE_DIR}/../acl" CACHE PATH "ACL v2.0.0 source root")
set(RTM_ROOT "${ACL_ROOT}/external/rtm" CACHE PATH "RTM source root")

add_library(acl_native SHARED src/acl_native.cpp)
target_include_directories(acl_native PRIVATE
	"${CMAKE_CURRENT_SOURCE_DIR}/include"
	"${ACL_ROOT}/includes"
	"${RTM_ROOT}/includes")

target_compile_definitions(acl_native PRIVATE ACLV_BUILD_DLL)
set_target_properties(acl_native PROPERTIES OUTPUT_NAME "acl-native")

if(MSVC)
	target_compile_options(acl_native PRIVATE /W3 /utf-8)
endif()
