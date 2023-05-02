
// Mostly copied from Eternal JK2 code
#include "minizip/include/minizip/ioapi.h"
#include "minizip/include/minizip/unzip.h"
#include <exception>
#include <xlocale>
#include <iostream>

#define	MAX_QPATH			64		// max length of a quake game pathname
#ifdef PATH_MAX
#define MAX_OSPATH			PATH_MAX
#else
#define	MAX_OSPATH			256		// max length of a filesystem pathname
#endif

#define MAX_ZPATH			256
#define	MAX_SEARCH_PATHS	4096
#define MAX_FILEHASH_SIZE	1024

#define	BIG_INFO_STRING		8192  // used for system info key only
#define	BIG_INFO_KEY		  8192
#define	BIG_INFO_VALUE		8192


#define	MAX_INFO_STRING		1024
#define	MAX_INFO_KEY		1024
#define	MAX_INFO_VALUE		1024

// referenced flags
// these are in loop specific order so don't change the order
#define FS_GENERAL_REF	0x01
#define FS_UI_REF		0x02
#define FS_CGAME_REF	0x04
// number of id paks that will never be autodownloaded from base
#define NUM_ID_PAKS		9

#define	MAX_FILE_HANDLES	256 // increased from 64 in jk2mv

/*
============================================================================

BYTE ORDER FUNCTIONS

============================================================================
*/

typedef union {
	float f;
	int i;
	unsigned int ui;
} floatint_t;

inline int16_t ShortSwap(int16_t l) {
	uint16_t us = *(uint16_t*)&l;

	return
		((us & 0x00FFu) << 8u) |
		((us & 0xFF00u) >> 8u);
}

inline int32_t LongSwap(int32_t l) {
	uint32_t ui = *(uint32_t*)&l;

	return
		((ui & 0x000000FFu) << 24u) |
		((ui & 0x0000FF00u) << 8u) |
		((ui & 0x00FF0000u) >> 8u) |
		((ui & 0xFF000000u) >> 24u);

}

inline float FloatSwap(const float* f) {
	floatint_t out;

	out.f = *f;
	out.i = LongSwap(out.i);

	return out.f;
}
// this is the define for determining if we have an asm version of a C function
#if (defined ARCH_X86)  && !defined __LCC__
#define id386	1
#else
#define id386	0
#endif

#if defined(ARCH_X86_64)  && !defined __LCC__
#define idx64	1
#else
#define idx64	0
#endif

#if defined(ARCH_ARM32) && !defined __LCC__
#define idarm32	1
#else
#define idarm32	0
#endif

#if (defined(powerc) || defined(powerpc) || defined(ppc) || defined(__ppc) || defined(__ppc__)) && !defined __LCC__
#define idppc	1
#else
#define idppc	0
#endif

#ifdef _MSC_VER

#define	QDECL	__cdecl
#define Q_INLINE __inline
#define Q_NORETURN __declspec(noreturn)
#define Q_PTR_NORETURN // MSVC doesn't support noreturn function pointers
#define q_unreachable() abort()
#ifndef __alignof_is_defined
#define alignof(x) __alignof(x)
#define __alignof_is_defined 1
#endif
#ifndef __alignas_is_defined
#define alignas(x) __declspec(align(x))
#define __alignas_is_defined 1
#endif
#define Q_MAX_ALIGN std::max_align_t
#define Q_EXPORT __declspec(dllexport)

#elif defined __GNUC__ && !defined __clang__

#define	QDECL
#define GCC_VERSION (__GNUC__ * 10000			\
    + __GNUC_MINOR__ * 100 \
    + __GNUC_PATCHLEVEL__)

#define Q_INLINE inline
#define Q_NORETURN __attribute__((noreturn))
#define Q_PTR_NORETURN Q_NORETURN
#define q_unreachable() __builtin_unreachable()
#if GCC_VERSION >= 40900 /* >= 4.9.0 */
#	define Q_MAX_ALIGN std::max_align_t
#else
#	define Q_MAX_ALIGN max_align_t
#endif
#define Q_EXPORT __attribute__((visibility("default")))

#elif defined __clang__

#define	QDECL
#define Q_INLINE inline
#define Q_NORETURN __attribute__((noreturn))
#define Q_PTR_NORETURN Q_NORETURN
#define q_unreachable() __builtin_unreachable()
#define Q_MAX_ALIGN std::max_align_t
#define Q_EXPORT __attribute__((visibility("default")))

#else

#define	QDECL
#define Q_INLINE inline
#define Q_NORETURN
#define Q_PTR_NORETURN
#define q_unreachable() abort()
#define Q_MAX_ALIGN std::max_align_t
#define Q_EXPORT

#endif

#if id386
#define ARCH_STRING "x86"
#define Q_LITTLE_ENDIAN
#elif idx64
#	ifdef WIN32
#		define ARCH_STRING "x64"
#	elif MACOS_X
#		define ARCH_STRING "x86_64"
#	else
#		define ARCH_STRING "amd64"
#	endif
#define Q_LITTLE_ENDIAN
#elif idarm32
#define ARCH_STRING "arm"
#define Q_LITTLE_ENDIAN
#elif idppc
#define ARCH_STRING "ppc"
#define Q_BIG_ENDIAN
#else
#error "Architecture not supported"
#endif

#if defined(Q_LITTLE_ENDIAN)
#define BigShort(x) ShortSwap(x)
#define BigLong(x) LongSwap(x)
#define BigFloat(x) FloatSwap(x)
#define LittleShort
#define LittleLong
#define LittleFloat
#endif

#if defined(Q_BIG_ENDIAN)
#define LittleShort(x) ShortSwap(x)
#define LittleLong(x) LongSwap(x)
#define LittleFloat(x) FloatSwap(x)
#define BigShort
#define BigLong
#define BigFloat
#endif

#ifdef WIN32

#define OS_STRING "win"
#define LIBRARY_EXTENSION "dll"
#define	PATH_SEP '\\'

#endif


typedef unsigned char		byte;
typedef unsigned short		word;
typedef unsigned long		ulong;

typedef enum { qfalse, qtrue }	qboolean;


typedef struct fileInPack_s {
	char* name;		// name of the file
	unsigned int			pos;		// file info position in zip
	unsigned int			len;		// uncompress file size
	struct	fileInPack_s* next;		// next file in the hash
} fileInPack_t;
typedef struct {
	char			pakFilename[MAX_OSPATH];	// c:\quake3\base\pak0.pk3
	char			pakBasename[MAX_OSPATH];	// pak0
	char			pakGamename[MAX_OSPATH];	// base
	unzFile			handle;						// handle to zip file
	int				checksum;					// regular checksum
	int				pure_checksum;				// checksum for pure
	int				numfiles;					// number of files in pk3
	int				referenced;					// referenced file flags
	qboolean		noref;						// file is blacklisted for referencing
	int				hashSize;					// hash table size (power of 2)
	fileInPack_t** hashTable;					// hash table
	fileInPack_t* buildBuffer;				// buffer with the filenames etc.
	int				gvc;						// game-version compatibility
} pack_t;


typedef struct {
	char		path[MAX_OSPATH];		// c:\jk2
	char		gamedir[MAX_OSPATH];	// base
} directory_t;
typedef struct searchpath_s {
	struct searchpath_s* next;

	pack_t* pack;		// only one of pack / dir will be non NULL
	directory_t* dir;
} searchpath_t;

static	int			fs_packFiles;			// total number of files in packs

static int fs_fakeChkSum;
static int fs_checksumFeed;
static	searchpath_t* fs_searchpaths;
static	int			fs_readCount;			// total bytes read
static	int			fs_loadCount;			// total files read
static	int			fs_loadStack;			// total files in memory


char* Q_strlwr(char* s1) {
	char* s;

	s = s1;
	while (*s) {
		*s = tolower(*s);
		s++;
	}
	return s1;
}
/*
=============
Q_strncpyz

Safe strncpy that ensures a trailing zero
=============
*/
void Q_strncpyz(char* dest, const char* src, int destsize) {
	if (destsize < 1) {
		throw std::exception("Q_strncpyz: destsize < 1");
		//Com_Error(ERR_FATAL, "Q_strncpyz: destsize < 1");
	}

	strncpy(dest, src, destsize - 1);
	dest[destsize - 1] = 0;
}

int Q_stricmpn(const char* s1, const char* s2, int n) {
	int		c1, c2;

	// bk001129 - moved in 1.17 fix not in id codebase
	if (s1 == NULL) {
		if (s2 == NULL)
			return 0;
		else
			return -1;
	}
	else if (s2 == NULL)
		return 1;



	do {
		c1 = *s1++;
		c2 = *s2++;

		if (!n--) {
			return 0;		// strings are equal until end point
		}

		if (c1 != c2) {
			if (c1 >= 'a' && c1 <= 'z') {
				c1 -= ('a' - 'A');
			}
			if (c2 >= 'a' && c2 <= 'z') {
				c2 -= ('a' - 'A');
			}
			if (c1 != c2) {
				return c1 < c2 ? -1 : 1;
			}
		}
	} while (c1);

	return 0;		// strings are equal
}

int Q_strncmp(const char* s1, const char* s2, int n) {
	int		c1, c2;

	do {
		c1 = *s1++;
		c2 = *s2++;

		if (!n--) {
			return 0;		// strings are equal until end point
		}

		if (c1 != c2) {
			return c1 < c2 ? -1 : 1;
		}
	} while (c1);

	return 0;		// strings are equal
}

int Q_stricmp(const char* s1, const char* s2) {
	return (s1 && s2) ? Q_stricmpn(s1, s2, 99999) : -1;
}
int Q_strlen(const char* s) {
	size_t l = strlen(s);

	if (l > INT_MAX) {
		throw  std::exception("Q_strlen: oversize string");
	}

	return l;
}
// never goes past bounds or leaves without a terminating 0
void Q_strcat(char* dest, int size, const char* src) {
	size_t		l1;

	l1 = Q_strlen(dest);
	if (l1 >= (size_t)size) {
		throw  std::exception("Q_strcat: already overflowed");
	}
	Q_strncpyz(dest + l1, src, size - (int)l1);
}




#if defined(_MSC_VER) && _MSC_VER < 1900
size_t Q_vsnprintf(char* str, size_t size, const char* format, va_list ap);
#else
#define Q_vsnprintf vsnprintf
#endif



/*
============
va

does a varargs printf into a temp buffer, so I don't need to have
varargs versions of all text functions.
FIXME: make this buffer size safe someday
============
*/
#define	MAX_VA_STRING	32000
#define MAX_VA_BUFFERS 4

char* QDECL va(const char* format, ...) {
	va_list		argptr;
	static char	string[MAX_VA_BUFFERS][MAX_VA_STRING];	// in case va is called by nested functions
	static int	index = 0;
	char* buf;

	va_start(argptr, format);
	buf = (char*)&string[index++ & 3];
	Q_vsnprintf(buf, MAX_VA_STRING, format, argptr);
	va_end(argptr);

	return buf;
}
#define	MAXPRINTMSG	4096
void Com_Printf(const char* fmt, ...) {
	va_list		argptr;
	char		msg[MAXPRINTMSG];

	va_start(argptr, fmt);
	//vsprintf(msg, fmt, argptr);
	vsprintf_s(msg, sizeof(msg), fmt, argptr);
	va_end(argptr);

	std::cout << "// " << msg;
}
void QDECL Com_sprintf(char* dest, int size, const char* fmt, ...) {
	size_t		len;
	va_list		argptr;

	if (size < 1) {
		throw std::exception("Com_sprintf: size < 1");
	}

	va_start(argptr, fmt);
	len = Q_vsnprintf(dest, size, fmt, argptr);
	va_end(argptr);

	if (len >= (size_t)size) {
		Com_Printf("Com_sprintf: overflow of %zu in %d\n", len, size);
	}
}

/* GLOBAL.H - RSAREF types and constants */

#include <string.h>
#include <fstream>

/* POINTER defines a generic pointer type */
typedef unsigned char* POINTER;

/* UINT2 defines a two byte word */
typedef unsigned short int UINT2;

/* UINT4 defines a four byte word */
typedef unsigned int UINT4;


/* MD4.H - header file for MD4C.C */

/* Copyright (C) 1991-2, RSA Data Security, Inc. Created 1991.

All rights reserved.

License to copy and use this software is granted provided that it is identified as the “RSA Data Security, Inc. MD4 Message-Digest Algorithm” in all material mentioning or referencing this software or this function.
License is also granted to make and use derivative works provided that such works are identified as “derived from the RSA Data Security, Inc. MD4 Message-Digest Algorithm” in all material mentioning or referencing the derived work.
RSA Data Security, Inc. makes no representations concerning either the merchantability of this software or the suitability of this software for any particular purpose. It is provided “as is” without express or implied warranty of any kind.

These notices must be retained in any copies of any part of this documentation and/or software. */

/* MD4 context. */
typedef struct {
	UINT4 state[4];				/* state (ABCD) */
	UINT4 count[2];				/* number of bits, modulo 2^64 (lsb first) */
	unsigned char buffer[64];			/* input buffer */
} MD4_CTX;

void MD4Init(MD4_CTX*);
void MD4Update(MD4_CTX*, const unsigned char*, unsigned int);
void MD4Final(unsigned char[16], MD4_CTX*);

static inline void Com_Memset(void* dest, const int val, const size_t count) {
	memset(dest, val, count);
}
static inline void Com_Memcpy(void* dest, const void* src, const size_t count) {
	memcpy(dest, src, count);
}

/* MD4C.C - RSA Data Security, Inc., MD4 message-digest algorithm */
/* Copyright (C) 1990-2, RSA Data Security, Inc. All rights reserved.

License to copy and use this software is granted provided that it is identified as the
RSA Data Security, Inc. MD4 Message-Digest Algorithm
 in all material mentioning or referencing this software or this function.
License is also granted to make and use derivative works provided that such works are identified as
derived from the RSA Data Security, Inc. MD4 Message-Digest Algorithm
in all material mentioning or referencing the derived work.
RSA Data Security, Inc. makes no representations concerning either the merchantability of this software or the suitability of this software for any particular purpose. It is provided
as is without express or implied warranty of any kind.

These notices must be retained in any copies of any part of this documentation and/or software. */

/* Constants for MD4Transform routine.  */
#define S11 3
#define S12 7
#define S13 11
#define S14 19
#define S21 3
#define S22 5
#define S23 9
#define S24 13
#define S31 3
#define S32 9
#define S33 11
#define S34 15

static void MD4Transform(UINT4[4], const unsigned char[64]);
static void Encode(unsigned char*, UINT4*, unsigned int);
static void Decode(UINT4*, const unsigned char*, unsigned int);

static const unsigned char PADDING[64] = { 0x80, 0 };

/* F, G and H are basic MD4 functions. */
#define F(x, y, z) (((x) & (y)) | ((~x) & (z)))
#define G(x, y, z) (((x) & (y)) | ((x) & (z)) | ((y) & (z)))
#define H(x, y, z) ((x) ^ (y) ^ (z))

/* ROTATE_LEFT rotates x left n bits. */
#define ROTATE_LEFT(x, n) (((x) << (n)) | ((x) >> (32-(n))))

/* FF, GG and HH are transformations for rounds 1, 2 and 3 */
/* Rotation is separate from addition to prevent recomputation */
#define FF(a, b, c, d, x, s) {(a) += F ((b), (c), (d)) + (x); (a) = ROTATE_LEFT ((a), (s));}

#define GG(a, b, c, d, x, s) {(a) += G ((b), (c), (d)) + (x) + (UINT4)0x5a827999; (a) = ROTATE_LEFT ((a), (s));}

#define HH(a, b, c, d, x, s) {(a) += H ((b), (c), (d)) + (x) + (UINT4)0x6ed9eba1; (a) = ROTATE_LEFT ((a), (s));}


/* MD4 initialization. Begins an MD4 operation, writing a new context. */
void MD4Init(MD4_CTX* context)
{
	context->count[0] = context->count[1] = 0;

	/* Load magic initialization constants.*/
	context->state[0] = 0x67452301;
	context->state[1] = 0xefcdab89;
	context->state[2] = 0x98badcfe;
	context->state[3] = 0x10325476;
}

/* MD4 block update operation. Continues an MD4 message-digest operation, processing another message block, and updating the context. */
void MD4Update(MD4_CTX* context, const unsigned char* input, unsigned int inputLen)
{
	unsigned int i, index, partLen;

	/* Compute number of bytes mod 64 */
	index = (unsigned int)((context->count[0] >> 3) & 0x3F);

	/* Update number of bits */
	if ((context->count[0] += ((UINT4)inputLen << 3)) < ((UINT4)inputLen << 3))
		context->count[1]++;

	context->count[1] += ((UINT4)inputLen >> 29);

	partLen = 64 - index;

	/* Transform as many times as possible.*/
	if (inputLen >= partLen)
	{
		Com_Memcpy(&context->buffer[index], input, partLen);
		MD4Transform(context->state, context->buffer);

		for (i = partLen; i + 63 < inputLen; i += 64)
			MD4Transform(context->state, &input[i]);

		index = 0;
	}
	else
		i = 0;

	/* Buffer remaining input */
	Com_Memcpy(&context->buffer[index], &input[i], inputLen - i);
}


/* MD4 finalization. Ends an MD4 message-digest operation, writing the the message digest and zeroizing the context. */
void MD4Final(unsigned char digest[16], MD4_CTX* context)
{
	unsigned char bits[8];
	unsigned int index, padLen;

	/* Save number of bits */
	Encode(bits, context->count, 8);

	/* Pad out to 56 mod 64.*/
	index = (unsigned int)((context->count[0] >> 3) & 0x3f);
	padLen = (index < 56) ? (56 - index) : (120 - index);
	MD4Update(context, PADDING, padLen);

	/* Append length (before padding) */
	MD4Update(context, bits, 8);

	/* Store state in digest */
	Encode(digest, context->state, 16);

	/* Zeroize sensitive information.*/
	Com_Memset((POINTER)context, 0, sizeof(*context));
}


/* MD4 basic transformation. Transforms state based on block. */
static void MD4Transform(UINT4 state[4], const unsigned char block[64])
{
	UINT4 a = state[0], b = state[1], c = state[2], d = state[3], x[16];

	Decode(x, block, 64);

	/* Round 1 */
	FF(a, b, c, d, x[0], S11);				/* 1 */
	FF(d, a, b, c, x[1], S12);				/* 2 */
	FF(c, d, a, b, x[2], S13);				/* 3 */
	FF(b, c, d, a, x[3], S14);				/* 4 */
	FF(a, b, c, d, x[4], S11);				/* 5 */
	FF(d, a, b, c, x[5], S12);				/* 6 */
	FF(c, d, a, b, x[6], S13);				/* 7 */
	FF(b, c, d, a, x[7], S14);				/* 8 */
	FF(a, b, c, d, x[8], S11);				/* 9 */
	FF(d, a, b, c, x[9], S12);				/* 10 */
	FF(c, d, a, b, x[10], S13);			/* 11 */
	FF(b, c, d, a, x[11], S14);			/* 12 */
	FF(a, b, c, d, x[12], S11);			/* 13 */
	FF(d, a, b, c, x[13], S12);			/* 14 */
	FF(c, d, a, b, x[14], S13);			/* 15 */
	FF(b, c, d, a, x[15], S14);			/* 16 */

	/* Round 2 */
	GG(a, b, c, d, x[0], S21);			/* 17 */
	GG(d, a, b, c, x[4], S22);			/* 18 */
	GG(c, d, a, b, x[8], S23);			/* 19 */
	GG(b, c, d, a, x[12], S24);			/* 20 */
	GG(a, b, c, d, x[1], S21);			/* 21 */
	GG(d, a, b, c, x[5], S22);			/* 22 */
	GG(c, d, a, b, x[9], S23);			/* 23 */
	GG(b, c, d, a, x[13], S24);			/* 24 */
	GG(a, b, c, d, x[2], S21);			/* 25 */
	GG(d, a, b, c, x[6], S22);			/* 26 */
	GG(c, d, a, b, x[10], S23);			/* 27 */
	GG(b, c, d, a, x[14], S24);			/* 28 */
	GG(a, b, c, d, x[3], S21);			/* 29 */
	GG(d, a, b, c, x[7], S22);			/* 30 */
	GG(c, d, a, b, x[11], S23);			/* 31 */
	GG(b, c, d, a, x[15], S24);			/* 32 */

	/* Round 3 */
	HH(a, b, c, d, x[0], S31);				/* 33 */
	HH(d, a, b, c, x[8], S32);			/* 34 */
	HH(c, d, a, b, x[4], S33);			/* 35 */
	HH(b, c, d, a, x[12], S34);			/* 36 */
	HH(a, b, c, d, x[2], S31);			/* 37 */
	HH(d, a, b, c, x[10], S32);			/* 38 */
	HH(c, d, a, b, x[6], S33);			/* 39 */
	HH(b, c, d, a, x[14], S34);			/* 40 */
	HH(a, b, c, d, x[1], S31);			/* 41 */
	HH(d, a, b, c, x[9], S32);			/* 42 */
	HH(c, d, a, b, x[5], S33);			/* 43 */
	HH(b, c, d, a, x[13], S34);			/* 44 */
	HH(a, b, c, d, x[3], S31);			/* 45 */
	HH(d, a, b, c, x[11], S32);			/* 46 */
	HH(c, d, a, b, x[7], S33);			/* 47 */
	HH(b, c, d, a, x[15], S34);			/* 48 */

	state[0] += a;
	state[1] += b;
	state[2] += c;
	state[3] += d;

	/* Zeroize sensitive information.*/
	Com_Memset((POINTER)x, 0, sizeof(x));
}


/* Encodes input (UINT4) into output (unsigned char). Assumes len is a multiple of 4. */
static void Encode(unsigned char* output, UINT4* input, unsigned int len)
{
	unsigned int i, j;

	for (i = 0, j = 0; j < len; i++, j += 4) {
		output[j] = (unsigned char)(input[i] & 0xff);
		output[j + 1] = (unsigned char)((input[i] >> 8) & 0xff);
		output[j + 2] = (unsigned char)((input[i] >> 16) & 0xff);
		output[j + 3] = (unsigned char)((input[i] >> 24) & 0xff);
	}
}


/* Decodes input (unsigned char) into output (UINT4). Assumes len is a multiple of 4. */
static void Decode(UINT4* output, const unsigned char* input, unsigned int len)
{
	unsigned int i, j;

	for (i = 0, j = 0; j < len; i++, j += 4)
		output[i] = ((UINT4)input[j]) | (((UINT4)input[j + 1]) << 8) | (((UINT4)input[j + 2]) << 16) | (((UINT4)input[j + 3]) << 24);
}

//===================================================================

unsigned Com_BlockChecksum(const void* buffer, int length)
{
	int			digest[4];
	unsigned	val;
	MD4_CTX		ctx;

	MD4Init(&ctx);
	MD4Update(&ctx, (const unsigned char*)buffer, length);
	MD4Final((unsigned char*)digest, &ctx);

	val = digest[0] ^ digest[1] ^ digest[2] ^ digest[3];

	return val;
}

unsigned Com_BlockChecksumKey(void* buffer, int length, int key)
{
	int			digest[4];
	unsigned	val;
	MD4_CTX		ctx;

	MD4Init(&ctx);
	MD4Update(&ctx, (unsigned char*)&key, 4);
	MD4Update(&ctx, (unsigned char*)buffer, length);
	MD4Final((unsigned char*)digest, &ctx);

	val = digest[0] ^ digest[1] ^ digest[2] ^ digest[3];

	return val;
}


/*
================
return a hash value for the filename
================
*/
static int FS_HashFileName(const char* fname, int hashSize) {
	int		i;
	int	hash;
	char	letter;

	hash = 0;
	i = 0;
	while (fname[i] != '\0') {
		letter = tolower(fname[i]);
		if (letter == '.') break;				// don't include extension
		if (letter == '\\') letter = '/';		// damn path names
		if (letter == PATH_SEP) letter = '/';		// damn path names
		hash += (int)(letter) * (i + 119);
		i++;
	}
	hash = (hash ^ (hash >> 10) ^ (hash >> 20));
	hash &= (hashSize - 1);
	return hash;
}



// stripped down to bare minimum
static pack_t* FS_LoadZipFile(char* zipfile, const char* basename, int** headerLongsPtr, int* headerLongsNum)
{
	fileInPack_t* buildBuffer;
	pack_t* pack;
	unzFile			uf;
	int				err;
	unz_global_info gi;
	char			filename_inzip[MAX_ZPATH];
	unz_file_info	file_info;
	ZPOS64_T		i;
	size_t			len;
	int			hash;
	int				fs_numHeaderLongs;
	int* fs_headerLongs;
	char* namePtr;

	fs_numHeaderLongs = 0;

	uf = unzOpen(zipfile);
	err = unzGetGlobalInfo(uf, &gi);

	if (err != UNZ_OK)
		return NULL;

	fs_packFiles += gi.number_entry;

	len = 0;
	unzGoToFirstFile(uf);
	for (i = 0; i < gi.number_entry; i++)
	{
		err = unzGetCurrentFileInfo(uf, &file_info, filename_inzip, sizeof(filename_inzip), NULL, 0, NULL, 0);
		if (err != UNZ_OK) {
			break;
		}
		len += strlen(filename_inzip) + 1;
		unzGoToNextFile(uf);
	}

	buildBuffer = (struct fileInPack_s*)malloc((int)((gi.number_entry * sizeof(fileInPack_t)) + len)); // Yes it will leak but this is a stupid little tool, we don't care.
	namePtr = ((char*)buildBuffer) + gi.number_entry * sizeof(fileInPack_t);
	fs_headerLongs = (int*)malloc(gi.number_entry * sizeof(int));

	// get the hash table size from the number of files in the zip
	// because lots of custom pk3 files have less than 32 or 64 files
	for (i = 1; i <= MAX_FILEHASH_SIZE; i <<= 1) {
		if (i > gi.number_entry) {
			break;
		}
	}

	pack = (pack_t*)malloc(sizeof(pack_t) + i * sizeof(fileInPack_t*));
	pack->hashSize = i;
	pack->hashTable = (fileInPack_t**)(((char*)pack) + sizeof(pack_t));
	for (int i = 0; i < pack->hashSize; i++) {
		pack->hashTable[i] = NULL;
	}

	Q_strncpyz(pack->pakFilename, zipfile, sizeof(pack->pakFilename));
	Q_strncpyz(pack->pakBasename, basename, sizeof(pack->pakBasename));

	// strip .pk3 if needed
	if (strlen(pack->pakBasename) > 4 && !Q_stricmp(pack->pakBasename + strlen(pack->pakBasename) - 4, ".pk3")) {
		pack->pakBasename[strlen(pack->pakBasename) - 4] = 0;
	}

	pack->handle = uf;
	pack->numfiles = gi.number_entry;
	unzGoToFirstFile(uf);

	for (i = 0; i < gi.number_entry; i++)
	{
		err = unzGetCurrentFileInfo(uf, &file_info, filename_inzip, sizeof(filename_inzip), NULL, 0, NULL, 0);
		if (err != UNZ_OK) {
			break;
		}
		if (file_info.uncompressed_size > 0) {
			fs_headerLongs[fs_numHeaderLongs++] = LittleLong(file_info.crc);
		}
		Q_strlwr(filename_inzip);
		hash = FS_HashFileName(filename_inzip, pack->hashSize);
		buildBuffer[i].name = namePtr;
		strcpy(buildBuffer[i].name, filename_inzip);
		namePtr += strlen(filename_inzip) + 1;
		// store the file position in the zip
		buildBuffer[i].pos = unzGetOffset(uf);
		buildBuffer[i].len = file_info.uncompressed_size;
		buildBuffer[i].next = pack->hashTable[hash];
		pack->hashTable[hash] = &buildBuffer[i];
		unzGoToNextFile(uf);
	}

	pack->checksum = Com_BlockChecksum(fs_headerLongs, 4 * fs_numHeaderLongs);
	pack->pure_checksum = Com_BlockChecksumKey(fs_headerLongs, 4 * fs_numHeaderLongs, LittleLong(fs_checksumFeed));
	pack->checksum = LittleLong(pack->checksum);
	pack->pure_checksum = LittleLong(pack->pure_checksum);


	*headerLongsPtr = fs_headerLongs;
	*headerLongsNum = fs_numHeaderLongs;
	//free(fs_headerLongs);

	pack->buildBuffer = buildBuffer;

	/*
	// which versions does this pk3 support?

	// filename prefixes
	if (!Q_stricmpn(basename, "o102_", 5)) {
		pack->gvc = PACKGVC_1_02;
	}
	else if (!Q_stricmpn(basename, "o103_", 5)) {
		pack->gvc = PACKGVC_1_03;
	}
	else if (!Q_stricmpn(basename, "o104_", 5)) {
		pack->gvc = PACKGVC_1_04;
	}

	// mv.info file in root directory of pk3 file
	char cversion[128];
	int cversionlen = FS_PakReadFile(pack, "mv.info", cversion, sizeof(cversion) - 1);
	if (cversionlen) {
		cversion[cversionlen] = '\0';
		pack->gvc = PACKGVC_UNKNOWN; // mv.info file overwrites version prefixes

		if (Q_stristr(cversion, "compatible 1.02")) {
			pack->gvc |= PACKGVC_1_02;
		}

		if (Q_stristr(cversion, "compatible 1.03")) {
			pack->gvc |= PACKGVC_1_03;
		}

		if (Q_stristr(cversion, "compatible 1.04")) {
			pack->gvc |= PACKGVC_1_04;
		}

		if (Q_stristr(cversion, "compatible all")) {
			pack->gvc = PACKGVC_1_02 | PACKGVC_1_03 | PACKGVC_1_04;
		}
	}

	// assets are hardcoded
	if (!Q_stricmp(pack->pakBasename, "assets0")) {
		pack->gvc = PACKGVC_1_02 | PACKGVC_1_03 | PACKGVC_1_04;
	}
	else if (!Q_stricmp(pack->pakBasename, "assets1")) {
		pack->gvc = PACKGVC_1_02 | PACKGVC_1_03 | PACKGVC_1_04;
	}
	else if (!Q_stricmp(pack->pakBasename, "assets2")) {
		pack->gvc = PACKGVC_1_03 | PACKGVC_1_04;
	}
	else if (!Q_stricmp(pack->pakBasename, "assets5")) {
		pack->gvc = PACKGVC_1_04;
	}

	// never reference assetsmv files
	if (!Q_stricmpn(pack->pakBasename, "assetsmv", 8)) {
		pack->noref = qtrue;
	}*/

	return pack;
}


const char* FS_ReferencedPakPureChecksums(void) {
	static char	info[BIG_INFO_STRING];
	searchpath_t* search;
	int nFlags, numPaks, checksum;

	info[0] = 0;

	checksum = fs_checksumFeed;
	numPaks = 0;
	for (nFlags = FS_CGAME_REF; nFlags; nFlags = nFlags >> 1) {
		if (nFlags & FS_GENERAL_REF) {
			// add a delimter between must haves and general refs
			//Q_strcat(info, sizeof(info), "@ ");
			info[strlen(info) + 1] = '\0';
			info[strlen(info) + 2] = '\0';
			info[strlen(info)] = '@';
			info[strlen(info)] = ' ';
		}
		for (search = fs_searchpaths; search; search = search->next) {
			// is the element a pak file and has it been referenced based on flag?
			if (search->pack && (search->pack->referenced & nFlags)) {
				/*if (MV_GetCurrentGameversion() == VERSION_1_02 && (!Q_stricmp(search->pack->pakBasename, "assets2") || !Q_stricmp(search->pack->pakBasename, "assets5")))
					continue;

				if (MV_GetCurrentGameversion() == VERSION_1_03 && (!Q_stricmp(search->pack->pakBasename, "assets5")))
					continue;*/

				Q_strcat(info, sizeof(info), va("%i ", search->pack->pure_checksum));
				if (nFlags & (FS_CGAME_REF | FS_UI_REF)) {
					break;
				}
				checksum ^= search->pack->pure_checksum;
				numPaks++;
			}
		}
		if (fs_fakeChkSum != 0) {
			// only added if a non-pure file is referenced
			Q_strcat(info, sizeof(info), va("%i ", fs_fakeChkSum));
		}
	}
	// last checksum is the encoded number of referenced pk3s
	checksum ^= numPaks;
	Q_strcat(info, sizeof(info), va("%i ", checksum));

	return info;
}

void CL_SendPureChecksums(void) {
	const char* pChecksums;
	char cMsg[MAX_INFO_VALUE];
	int i;

	// if we are pure we need to send back a command with our referenced pk3 checksums
	pChecksums = FS_ReferencedPakPureChecksums();

	// "cp"
	// "Yf"
	Com_sprintf(cMsg, sizeof(cMsg), "Yf ");
	Q_strcat(cMsg, sizeof(cMsg), pChecksums);
	for (i = 0; i < 2; i++) {
		cMsg[i] += 10;
	}
	//CL_AddReliableCommand(cMsg);
}














int main(int argc,char** argv) {
	if (argc < 2) {
		throw std::exception("no file specified");
	}
	else {
		int countFiles = argc - 1;
		for (int i = 0; i < countFiles; i++) {
			char* fileName = argv[i + 1];
			char* fileNameJson = new char[strlen(fileName)+2];
			char* fileNamePcs = new char[strlen(fileName)+1];
			strcpy(fileNameJson, fileName);
			strcpy(fileNamePcs, fileName);
			int theStrLen = strlen(fileNameJson);
			fileNameJson[theStrLen + 1] = 0;
			fileNameJson[theStrLen] = 'n';
			fileNameJson[theStrLen -1] = 'o';
			fileNameJson[theStrLen -2] = 's';
			fileNameJson[theStrLen -3] = 'j';
			fileNamePcs[theStrLen -1] = 0;
			fileNamePcs[theStrLen -2] = 'l';
			fileNamePcs[theStrLen -3] = 'h';

			int* headerLongs = NULL;
			int headerLongCount = 0;
			pack_t* pack =FS_LoadZipFile(fileName,"whatever",&headerLongs, &headerLongCount);
			std::cout << "file: " << fileName << "\n";

			std::ofstream jsonFile(fileNameJson);
			jsonFile << "[";
			for (int c = 0; c < headerLongCount; c++) {
				if (c != 0) {
					jsonFile << ",";
				}
				jsonFile << headerLongs[c];
			}
			jsonFile << "]";
			jsonFile.close();

			FILE* binFile = fopen(fileNamePcs, "wb");

			fwrite(headerLongs,4,headerLongCount,binFile);
			fclose(binFile);

			//std::cout << "checksum: " << pack->checksum << "\n";
			//std::cout << "pure_checksum: " << pack->pure_checksum << "\n";
			std::cout << "\n\n";
			delete[] fileNameJson;
		}
	}
}